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

    public ActionResult Users()
    {
        var model = new UsersViewModel();

        // LINQ lấy danh sách Instructors
        model.Instructors = (from inst in db.Instructors
                             join acc in db.User_Accounts on inst.UserID equals acc.UserID
                             select new UserDto
                             {
                                 UserID = acc.UserID,
                                 DetailID = inst.InstructorID,
                                 FullName = inst.FullName,
                                 Email = acc.Email,
                                 Bio = inst.Bio,
                                 Status = acc.Status
                             }).ToList();

        // LINQ lấy danh sách Students
        model.Students = (from std in db.Students
                          join acc in db.User_Accounts on std.UserID equals acc.UserID
                          select new UserDto
                          {
                              UserID = acc.UserID,
                              DetailID = std.StudentID,
                              FullName = std.FullName,
                              Email = acc.Email,
                              Status = acc.Status
                          }).ToList();

        return View(model);
    }

    // 2. AJAX API: Đổi trạng thái từ Active <-> Inactive
    [HttpPost]
    public JsonResult ToggleUserStatus(int userId)
    {
        try
        {
            var account = db.User_Accounts.FirstOrDefault(u => u.UserID == userId);
            if (account == null)
            {
                return Json(new { success = false, message = "User account not found." });
            }

            // Chuyển đổi qua lại giữa Active và Inactive
            if (account.Status == "Active")
            {
                account.Status = "Inactive";
            }
            else
            {
                account.Status = "Active";
            }

            db.SubmitChanges(); // Lưu vào SQL Server

            return Json(new { success = true, newStatus = account.Status });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Error: " + ex.Message });
        }
    }

        // ==================== QUẢN LÝ INSTRUCTOR ====================

        // 1. Get: Hiển thị form chỉnh sửa Instructor
    public ActionResult EditInstructor(int id)
    {
        var instructor = db.Instructors.FirstOrDefault(i => i.InstructorID == id);
        if (instructor == null) return HttpNotFound();

        return View(instructor);
    }

    // 2. Post: Xử lý cập nhật thông tin Instructor
    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult EditInstructor(Instructor model)
    {
        if (ModelState.IsValid)
        {
            var instructor = db.Instructors.FirstOrDefault(i => i.InstructorID == model.InstructorID);
            if (instructor != null)
            {
                instructor.FullName = model.FullName;
                instructor.Phone = model.Phone;
                instructor.Address = model.Address;
                instructor.Bio = model.Bio;

                db.SubmitChanges();
                return RedirectToAction("Users");
            }
        }
        return View(model);
    }


    // ==================== QUẢN LÝ STUDENT ====================

    // 3. Get: Hiển thị form chỉnh sửa Student
    public ActionResult EditStudent(int id)
    {
        var student = db.Students.FirstOrDefault(s => s.StudentID == id);
        if (student == null) return HttpNotFound();

        return View(student);
    }

    // 4. Post: Xử lý cập nhật thông tin Student
    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult EditStudent(Student model)
    {
        if (ModelState.IsValid)
        {
            var student = db.Students.FirstOrDefault(s => s.StudentID == model.StudentID);
            if (student != null)
            {
                student.FullName = model.FullName;
                student.Phone = model.Phone;
                student.Address = model.Address;

                db.SubmitChanges();
                return RedirectToAction("Users");
            }
        }
        return View(model);
    }


}



    


