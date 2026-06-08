using OnlineCourseWebsite.Models;
using OnlineCourseWebsite.ViewModels;
using System;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Web;
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
        var viewModel = new CoursesViewModel();

        // 1. Lấy danh sách khóa học (Double Join để lấy cả CategoryName lẫn StatusName)
        viewModel.Courses = (from c in db.Courses
                             join cat in db.Categories on c.CategoryID equals cat.CategoryID
                             join st in db.CourseStatus on c.StatusID equals st.StatusID // 👈 JOIN THÊM BẢNG STATUS NÀY NÍ
                             select new CourseDisplayDto
                             {
                                 CourseID = c.CourseID,
                                 CourseName = c.CourseName,
                                 Price = c.Price ?? 0,
                                 CategoryName = cat.CategoryName,
                                 CategoryID = c.CategoryID,
                                 InstructorID = c.InstructorID,
                                 StatusID = c.StatusID,
                                 StatusName = st.StatusName, // 👈 GÁN THẰNG NÀY ĐỂ VIEW HỨNG NHA!
                                 Duration = c.Duration
                             }).ToList();

        // 2. Lấy danh sách Categories và Instructors
        viewModel.Categories = db.Categories.ToList();
        viewModel.Instructors = db.Instructors.ToList();

        // 3. Nạp dữ liệu đổ vào Dropdown trên Modal:
        viewModel.CourseStatuses = db.CourseStatus.ToList();

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
    // POST: Admin/CreateCourse
    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult CreateCourse(Course model, HttpPostedFileBase ImageFile)
    {
        try
        {
            // Gỡ lỗi ModelState nếu dính trường hợp property Image bị yêu cầu bắt buộc
            if (ModelState.ContainsKey("Image")) ModelState["Image"].Errors.Clear();

            if (ModelState.IsValid)
            {
                Course newCourse = new Course
                {
                    CourseName = model.CourseName.Trim(),
                    Description = model.Description?.Trim(),
                    Price = model.Price,
                    Duration = model.Duration?.Trim(),
                    CreatedDate = System.DateTime.Now,
                    InstructorID = model.InstructorID,
                    CategoryID = model.CategoryID,
                    StatusID = model.StatusID
                };

                // CHỖ NÀY NÈ: Xử lý lưu ảnh khi TẠO MỚI khóa học
                if (ImageFile != null && ImageFile.ContentLength > 0)
                {
                    string extension = Path.GetExtension(ImageFile.FileName);
                    string imageName = "course_" + Guid.NewGuid().ToString().Substring(0, 8) + extension;
                    string path = Path.Combine(Server.MapPath("~/Content/images/courses/"), imageName);

                    ImageFile.SaveAs(path);
                    newCourse.Image = imageName; // Gán tên file vào DB
                }

                db.Courses.InsertOnSubmit(newCourse);
                db.SubmitChanges();

                return RedirectToAction("Courses");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Lỗi Create Course: " + ex.Message);
        }

        return RedirectToAction("Courses");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult EditCourse(Course model, HttpPostedFileBase EditImageFile, string OldImage)
    {
        try
        {
            // Ép buộc xóa check validate của cột Image nếu form truyền về bị null trúng nó
            if (ModelState.ContainsKey("Image")) ModelState["Image"].Errors.Clear();

            if (ModelState.IsValid)
            {
                var course = db.Courses.SingleOrDefault(c => c.CourseID == model.CourseID);

                if (course != null)
                {
                    course.CourseName = model.CourseName.Trim();
                    course.Description = model.Description?.Trim();
                    course.Price = model.Price;
                    course.Duration = model.Duration?.Trim();
                    course.CategoryID = model.CategoryID;
                    course.InstructorID = model.InstructorID;
                    course.StatusID = model.StatusID;

                    // Xử lý ảnh nếu Admin upload ảnh mới thay thế
                    if (EditImageFile != null && EditImageFile.ContentLength > 0)
                    {
                        string extension = Path.GetExtension(EditImageFile.FileName);
                        string imageName = "course_" + Guid.NewGuid().ToString().Substring(0, 8) + extension;
                        string path = Path.Combine(Server.MapPath("~/Content/images/"), imageName);

                        EditImageFile.SaveAs(path);

                        // Cập nhật giá trị mới
                        course.Image = imageName;
                    }
                    else
                    {
                        // Giữ nguyên ảnh cũ nếu không change thumbnail
                        course.Image = OldImage;
                    }

                    // Chốt hạ đẩy dữ liệu xuống SQL Server
                    db.SubmitChanges();
                }
                return RedirectToAction("Courses");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Lỗi Edit Course: " + ex.Message);
        }

        return RedirectToAction("Courses");
    }

    public ActionResult Transactions()
    {
        var transactions = (from p in db.Payments
                            join e in db.Enrollments on p.EnrollmentID equals e.EnrollmentID
                            join s in db.Students on e.StudentID equals s.StudentID
                            join c in db.Courses on e.CourseID equals c.CourseID
                            orderby p.PaymentDate descending
                            select new AdminPaymentDisplayDto
                            {
                                PaymentID = p.PaymentID,
                                EnrollmentID = e.EnrollmentID,
                                StudentName = s.FullName,
                                CourseName = c.CourseName,
                                Amount = p.Amount,
                                PaymentMethod = p.PaymentMethod,
                                PaymentStatus = p.PaymentStatus,
                                PaymentDate = p.PaymentDate ?? DateTime.Now,
                                Progress = (double)(e.Progress ?? 0)
                            }).ToList();
        return View(transactions);
    }

    [HttpPost]
    public JsonResult ApprovePayment(int paymentId)
    {
        // Thêm dòng log này để biết nó có chạy vào hàm không
        System.Diagnostics.Debug.WriteLine("Đã vào được hàm với ID: " + paymentId);

        var payment = db.Payments.SingleOrDefault(p => p.PaymentID == paymentId);
        if (payment == null) return Json(new { success = false, message = "Không tìm thấy!" });

        payment.PaymentStatus = "Paid";
        db.SubmitChanges();
        return Json(new { success = true, message = "Duyệt thành công!" });
    }

    [HttpPost]
    public JsonResult CancelPayment(int paymentId)
    {
        var data = (from p in db.Payments
                    join e in db.Enrollments on p.EnrollmentID equals e.EnrollmentID
                    where p.PaymentID == paymentId
                    select new { Payment = p, Enrollment = e }).SingleOrDefault();

        if (data == null) return Json(new { success = false, message = "Not found!" });

        // Logic "thép": Chỉ cho huỷ nếu Pending HOẶC (Paid + <24h + <5% Progress)
        bool isPending = data.Payment.PaymentStatus == "Pending";
        bool canRefund = (data.Payment.PaymentStatus == "Paid" &&
                         (DateTime.Now - data.Payment.PaymentDate.Value).TotalHours <= 24 &&
                         data.Enrollment.Progress < 5);

        if (!isPending && !canRefund)
            return Json(new { success = false, message = "Cannot cancel: Course is in progress or refund window closed." });

        data.Payment.PaymentStatus = "Cancelled";
        db.SubmitChanges();
        return Json(new { success = true, message = "Cancelled successfully." });
    }

}



    


