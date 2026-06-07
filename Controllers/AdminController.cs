using OnlineCourseWebsite.Models;
using OnlineCourseWebsite.ViewModels;
using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;

public class AdminController : Controller
{
    dbOnlineCourseDataContext db = new dbOnlineCourseDataContext();
    private string connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;

    // Trang chủ Admin (Dashboard)
    public ActionResult Dashboard()
    {
        // --- GIỮ NGUYÊN MẤY DÒNG ĐẾM CŨ ---
        ViewBag.TotalCourses = db.Courses.Count();
        ViewBag.TotalStudents = db.Students.Count();
        ViewBag.TotalInstructors = db.Instructors.Count();
        ViewBag.TotalEnrollments = db.Enrollments.Count();
        ViewBag.TotalRevenue = db.Payments.Where(p => p.PaymentStatus == "Paid").Sum(p => (decimal?)p.Amount).GetValueOrDefault(0);

        // --- XỬ LÝ REVIEW THẬT CHỖ NÀY NÈ NÍ ---
        // 1. Tính Rating trung bình của toàn hệ thống (nếu chưa có review nào thì mặc định là 5.0)
        var allReviews = db.Reviews.ToList();
        double avgRating = allReviews.Any() ? allReviews.Average(r => r.Rating) : 5.0;
        ViewBag.AvgRating = avgRating.ToString("F1"); // Ép về 1 chữ số thập phân (Ví dụ: 4.6)

        // 2. Lấy danh sách Reviews mới nhất để hiển thị ra Dashboard
        // Vì ném thẳng qua View, ní cứ `.OrderByDescending` theo ngày cho chuẩn bài
        var reviewList = db.Reviews
                           .OrderByDescending(r => r.ReviewDate)
                           .Take(5) // Lấy 5 cái mới nhất thôi cho đỡ chật Dashboard ghen ní
                           .ToList();

        return View(reviewList); // Truyền nguyên cái List Review này làm Model chính của trang
    }

    public ActionResult Courses()
    {
        // LINQ Query JOIN thuần, gọi đúng tên bảng không có "s"
        var courseQuery = from c in db.Courses
                          join cat in db.Categories on c.CategoryID equals cat.CategoryID
                          join stat in db.CourseStatus on c.StatusID equals stat.StatusID
                          join inst in db.Instructors on c.InstructorID equals inst.InstructorID
                          select new CourseDisplayDto
                          {
                              CourseID = c.CourseID,
                              CourseName = c.CourseName,
                              Price = c.Price ?? 0,
                              CategoryName = cat.CategoryName,
                              InstructorName = inst.FullName,
                              InstructorID = c.InstructorID,
                              StatusName = stat.StatusName, // Active, Pending...
                              StatusID = c.StatusID
                          };

        var viewModel = new CoursesViewModel
        {
            Courses = courseQuery.ToList(),
            Categories = db.Categories.ToList(), // Gọi đúng tên tập hợp trong dbml
            Instructors = db.Instructors.ToList()
        };

        return View(viewModel);
    }

    // POST: Admin/CreateCategory
    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult CreateCategory(string categoryName)
    {
        if (!string.IsNullOrEmpty(categoryName))
        {
            bool isExist = db.Categories.Any(c => c.CategoryName.ToLower() == categoryName.Trim().ToLower());
            if (!isExist)
            {
                // Khởi tạo đối tượng từ class LINQ to SQL
                Category cat = new Category { CategoryName = categoryName.Trim() };

                // THUẦN LINQ TO SQL: Dùng InsertOnSubmit thay cho Add
                db.Categories.InsertOnSubmit(cat);

                // THUẦN LINQ TO SQL: SubmitChanges thần thánh
                db.SubmitChanges();
            }
        }
        return RedirectToAction("Courses");
    }

    // POST: Admin/DeleteCategory
    [HttpPost]
    public ActionResult DeleteCategory(int id)
    {
        // THUẦN LINQ TO SQL: Không dùng Find(), dùng SingleOrDefault
        var cat = db.Categories.SingleOrDefault(c => c.CategoryID == id);
        if (cat != null)
        {
            bool hasCourse = db.Courses.Any(c => c.CategoryID == id);
            if (hasCourse)
            {
                return Json(new { success = false, message = "Cannot delete! This category contains active courses." });
            }

            // THUẦN LINQ TO SQL: Dùng DeleteOnSubmit
            db.Categories.DeleteOnSubmit(cat);
            db.SubmitChanges();
            return Json(new { success = true });
        }
        return Json(new { success = false, message = "Category not found." });
    }

    // AJAX POST: Cập nhật nhanh Instructor khi Admin thay đổi dropdown
    [HttpPost]
    public ActionResult UpdateInstructorAssignment(int courseId, int instructorId)
    {
        // THUẦN LINQ TO SQL: Dùng SingleOrDefault để bốc row ra update
        var course = db.Courses.SingleOrDefault(c => c.CourseID == courseId);
        if (course != null)
        {
            course.InstructorID = instructorId;
            db.SubmitChanges(); // Cập nhật trực tiếp
            return Json(new { success = true });
        }
        return Json(new { success = false });
    }


    // Trang duyệt thanh toán (Cái này quan trọng nè!)
    public ActionResult ManagePayments()
    {
        // Lấy danh sách hóa đơn đang "Awaiting" hoặc "Pending"
        var payments = db.Payments
                         .Where(p => p.PaymentStatus == "Awaiting" || p.PaymentStatus == "Pending")
                         .OrderByDescending(p => p.PaymentDate)
                         .ToList();
        return View(payments);
    }

    // GET: Admin/Users
        public ActionResult Users()
    {
        var model = new UserManagementViewModel();

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            conn.Open();

            // 1. Đọc danh sách Instructor
            string queryInstructors = @"
                    SELECT i.InstructorID, i.UserID, i.FullName, i.Bio, u.Email, u.Status 
                    FROM Instructor i
                    INNER JOIN User_Account u ON i.UserID = u.UserID";
            using (SqlCommand cmd = new SqlCommand(queryInstructors, conn))
            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    model.Instructors.Add(new InstructorUserDTO
                    {
                        InstructorID = (int)reader["InstructorID"],
                        UserID = (int)reader["UserID"],
                        FullName = reader["FullName"].ToString(),
                        Email = reader["Email"].ToString(),
                        Bio = reader["Bio"] != DBNull.Value ? reader["Bio"].ToString() : "",
                        Status = reader["Status"].ToString()
                    });
                }
            }

            // 2. Đọc danh sách Student
            string queryStudents = @"
                    SELECT s.StudentID, s.UserID, s.FullName, u.Email, u.Status 
                    FROM Student s
                    INNER JOIN User_Account u ON s.UserID = u.UserID";
            using (SqlCommand cmd = new SqlCommand(queryStudents, conn))
            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    model.Students.Add(new StudentUserDTO
                    {
                        StudentID = (int)reader["StudentID"],
                        UserID = (int)reader["UserID"],
                        FullName = reader["FullName"].ToString(),
                        Email = reader["Email"].ToString(),
                        Status = reader["Status"].ToString()
                    });
                }
            }
        }

        return View(model);
    }

    // POST: Admin/ToggleUserStatus
    [HttpPost]
    public JsonResult ToggleUserStatus(int userId)
    {
        try
        {
            string currentStatus = "";
            string newStatus = "";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Lấy trạng thái hiện tại
                string selectQuery = "SELECT Status FROM User_Account WHERE UserID = @UserID";
                using (SqlCommand cmd = new SqlCommand(selectQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@UserID", userId);
                    var result = cmd.ExecuteScalar();
                    if (result != null) currentStatus = result.ToString();
                }

                // Đảo ngược trạng thái dựa trên CHECK CONSTRAINT ('Active', 'Inactive')
                newStatus = (currentStatus == "Active") ? "Inactive" : "Active";

                // Cập nhật trạng thái mới
                string updateQuery = "UPDATE User_Account SET Status = @NewStatus WHERE UserID = @UserID";
                using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@NewStatus", newStatus);
                    cmd.Parameters.AddWithValue("@UserID", userId);
                    cmd.ExecuteNonQuery();
                }
            }

            return Json(new { success = true, newStatus = newStatus });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
}

