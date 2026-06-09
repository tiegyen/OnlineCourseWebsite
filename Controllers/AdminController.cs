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

    // Khai báo biến múi giờ Việt Nam dùng chung cho toàn Controller
    private readonly TimeZoneInfo vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    // Hàm tiện ích lấy nhanh giờ Việt Nam hiện tại hiện thời
    private DateTime GetVnNow()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone);
    }

    // Trang chủ Admin (Dashboard)
    public ActionResult Dashboard()
    {
        ViewBag.TotalCourses = db.Courses.Count();
        ViewBag.TotalStudents = db.Students.Count();
        ViewBag.TotalInstructors = db.Instructors.Count();
        ViewBag.TotalEnrollments = db.Enrollments.Count();
        ViewBag.TotalRevenue = db.Payments.Where(p => p.PaymentStatus == "Paid").Sum(p => (decimal?)p.Amount).GetValueOrDefault(0);

        var allReviews = db.Reviews.ToList();
        double avgRating = allReviews.Any() ? allReviews.Average(r => r.Rating) : 5.0;
        ViewBag.AvgRating = avgRating.ToString("F1");

        var reviewList = db.Reviews
                           .OrderByDescending(r => r.ReviewDate)
                           .Take(5)
                           .ToList();

        return View(reviewList);
    }

    public ActionResult Courses()
    {
        var viewModel = new CoursesViewModel();

        viewModel.Courses = (from c in db.Courses
                             join cat in db.Categories on c.CategoryID equals cat.CategoryID
                             join st in db.CourseStatus on c.StatusID equals st.StatusID
                             select new CourseDisplayDto
                             {
                                 CourseID = c.CourseID,
                                 CourseName = c.CourseName,
                                 Price = c.Price ?? 0,
                                 CategoryName = cat.CategoryName,
                                 CategoryID = c.CategoryID,
                                 InstructorID = c.InstructorID,
                                 StatusID = c.StatusID,
                                 StatusName = st.StatusName,
                                 Duration = c.Duration
                             }).ToList();

        viewModel.Categories = db.Categories.ToList();
        viewModel.Instructors = db.Instructors.ToList();
        viewModel.CourseStatuses = db.CourseStatus.ToList();

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult CreateCategory(string categoryName)
    {
        if (!string.IsNullOrEmpty(categoryName))
        {
            bool isExist = db.Categories.Any(c => c.CategoryName.ToLower() == categoryName.Trim().ToLower());
            if (!isExist)
            {
                Category cat = new Category { CategoryName = categoryName.Trim() };
                db.Categories.InsertOnSubmit(cat);
                db.SubmitChanges();
            }
        }
        return RedirectToAction("Courses");
    }

    [HttpPost]
    public ActionResult EditCategory(int id, string categoryName)
    {
        if (string.IsNullOrEmpty(categoryName))
        {
            return Json(new { success = false, message = "Category name cannot be empty." });
        }

        var cat = db.Categories.SingleOrDefault(c => c.CategoryID == id);
        if (cat == null)
        {
            return Json(new { success = false, message = "Category not found." });
        }

        bool isExist = db.Categories.Any(c => c.CategoryID != id && c.CategoryName.ToLower() == categoryName.Trim().ToLower());
        if (isExist)
        {
            return Json(new { success = false, message = "This category name already exists." });
        }

        try
        {
            cat.CategoryName = categoryName.Trim();
            db.SubmitChanges();
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Error: " + ex.Message });
        }
    }

    [HttpPost]
    public ActionResult DeleteCategory(int id)
    {
        var cat = db.Categories.SingleOrDefault(c => c.CategoryID == id);
        if (cat != null)
        {
            bool hasCourse = db.Courses.Any(c => c.CategoryID == id);
            if (hasCourse)
            {
                return Json(new { success = false, message = "Cannot delete! This category contains active courses." });
            }

            db.Categories.DeleteOnSubmit(cat);
            db.SubmitChanges();
            return Json(new { success = true });
        }
        return Json(new { success = false, message = "Category not found." });
    }

    [HttpPost]
    public ActionResult UpdateInstructorAssignment(int courseId, int instructorId)
    {
        var course = db.Courses.SingleOrDefault(c => c.CourseID == courseId);
        if (course != null)
        {
            course.InstructorID = instructorId;
            db.SubmitChanges();
            return Json(new { success = true });
        }
        return Json(new { success = false });
    }

    public ActionResult ManagePayments()
    {
        var payments = db.Payments
                         .Where(p => p.PaymentStatus == "Awaiting" || p.PaymentStatus == "Pending")
                         .OrderByDescending(p => p.PaymentDate)
                         .ToList();
        return View(payments);
    }

    public ActionResult Users()
    {
        var model = new UsersViewModel();

        // 1. Lấy danh sách Instructors kèm ảnh
        model.Instructors = (from inst in db.Instructors
                             join acc in db.User_Accounts on inst.UserID equals acc.UserID
                             select new UserDto
                             {
                                 UserID = acc.UserID,
                                 DetailID = inst.InstructorID,
                                 FullName = inst.FullName,
                                 Email = acc.Email,
                                 Bio = inst.Bio,
                                 Status = acc.Status,
                                 UserImage = inst.Image // Hoặc inst.Avatar tùy tên cột trong DB của ní
                             }).ToList();

        // 2. Lấy danh sách Students kèm ảnh
        model.Students = (from std in db.Students
                          join acc in db.User_Accounts on std.UserID equals acc.UserID
                          select new UserDto
                          {
                              UserID = acc.UserID,
                              DetailID = std.StudentID,
                              FullName = std.FullName,
                              Email = acc.Email,
                              Status = acc.Status,
                              UserImage = std.Avatar // Hoặc std.Avatar tùy tên cột trong DB của ní
                          }).ToList();

        return View(model);
    }

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

            account.Status = (account.Status == "Active") ? "Inactive" : "Active";
            db.SubmitChanges();

            return Json(new { success = true, newStatus = account.Status });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Error: " + ex.Message });
        }
    }

    public ActionResult EditInstructor(int id)
    {
        var instructor = db.Instructors.FirstOrDefault(i => i.InstructorID == id);
        if (instructor == null) return HttpNotFound();

        return View(instructor);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult EditInstructor(Instructor model, string NewPassword)
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

                if (!string.IsNullOrEmpty(NewPassword))
                {
                    var account = db.User_Accounts.FirstOrDefault(u => u.UserID == model.UserID);
                    if (account != null)
                    {
                        account.Password = NewPassword.Trim();
                    }
                }

                db.SubmitChanges();
                TempData["Success"] = "Instructor updated successfully!";
                return RedirectToAction("Users");
            }
        }
        return View(model);
    }

    public ActionResult EditStudent(int id)
    {
        var student = db.Students.FirstOrDefault(s => s.StudentID == id);
        if (student == null) return HttpNotFound();

        return View(student);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult EditStudent(Student model, string NewPassword)
    {
        if (ModelState.IsValid)
        {
            var student = db.Students.FirstOrDefault(s => s.StudentID == model.StudentID);
            if (student != null)
            {
                student.FullName = model.FullName;
                student.Phone = model.Phone;
                student.Address = model.Address;

                if (!string.IsNullOrEmpty(NewPassword))
                {
                    var account = db.User_Accounts.FirstOrDefault(u => u.UserID == model.UserID);
                    if (account != null)
                    {
                        account.Password = NewPassword.Trim();
                    }
                }

                db.SubmitChanges();
                TempData["Success"] = "Student updated successfully!";
                return RedirectToAction("Users");
            }
        }
        return View(model);
    }

    public ActionResult CreateInstructor()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult CreateInstructor(string Email, string Password, string FullName, string Phone, string Address, string Bio)
    {
        try
        {
            if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Password) || string.IsNullOrEmpty(FullName))
            {
                ModelState.AddModelError("", "Email, Password and Full Name are required.");
                return View();
            }

            bool isEmailExist = db.User_Accounts.Any(u => u.Email.ToLower() == Email.Trim().ToLower());
            if (isEmailExist)
            {
                ModelState.AddModelError("", "This email address is already registered!");
                return View();
            }

            User_Account newAccount = new User_Account
            {
                Email = Email.Trim(),
                Password = Password.Trim(),
                Role = "Instructor",
                Status = "Active"
            };

            db.User_Accounts.InsertOnSubmit(newAccount);
            db.SubmitChanges();

            Instructor newInstructor = new Instructor
            {
                UserID = newAccount.UserID,
                FullName = FullName.Trim(),
                Phone = Phone?.Trim(),
                Address = Address?.Trim(),
                Bio = Bio?.Trim()
            };

            db.Instructors.InsertOnSubmit(newInstructor);
            db.SubmitChanges();

            TempData["Success"] = "Instructor account created successfully!";
            return RedirectToAction("Users");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "Database error occurred: " + ex.Message);
            return View();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult CreateCourse(Course model, HttpPostedFileBase ImageFile)
    {
        try
        {
            if (model == null || string.IsNullOrEmpty(model.CourseName))
            {
                TempData["Error"] = "Tên khóa học không được để trống!";
                return RedirectToAction("Courses");
            }

            Course newCourse = new Course
            {
                CourseName = model.CourseName.Trim(),
                Description = model.Description?.Trim(),
                Price = model.Price,
                Duration = model.Duration?.Trim(),
                CreatedDate = GetVnNow(), // Ép sang giờ VN khi tạo khóa học mới
                InstructorID = model.InstructorID,
                CategoryID = model.CategoryID,
                StatusID = model.StatusID
            };

            if (ImageFile != null && ImageFile.ContentLength > 0)
            {
                string extension = Path.GetExtension(ImageFile.FileName);
                string imageName = "course_" + Guid.NewGuid().ToString().Substring(0, 8) + extension;
                string folderPath = Server.MapPath("~/Content/images/");

                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string path = Path.Combine(folderPath, imageName);
                ImageFile.SaveAs(path);
                newCourse.Image = imageName;
            }
            else
            {
                newCourse.Image = "default.png";
            }

            db.Courses.InsertOnSubmit(newCourse);
            db.SubmitChanges();

            TempData["Success"] = "Thêm khóa học thành công rồi ní ơi!";
            return RedirectToAction("Courses");
        }
        catch (System.Data.Linq.ForeignKeyReferenceAlreadyHasValueException ex)
        {
            System.Diagnostics.Debug.WriteLine("❌ LỖI KHÓA NGOẠI: " + ex.Message);
            TempData["Error"] = "Lỗi liên kết dữ liệu (Foreign Key).";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("❌ LỖI CREATE COURSE CHI TIẾT: " + ex.ToString());
            TempData["Error"] = "Không lưu được vào database! Lỗi: " + ex.Message;
        }

        return RedirectToAction("Courses");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult EditCourse(Course model, HttpPostedFileBase EditImageFile, string OldImage)
    {
        try
        {
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

                    if (EditImageFile != null && EditImageFile.ContentLength > 0)
                    {
                        string extension = Path.GetExtension(EditImageFile.FileName);
                        string imageName = "course_" + Guid.NewGuid().ToString().Substring(0, 8) + extension;
                        string path = Path.Combine(Server.MapPath("~/Content/images/"), imageName);
                        EditImageFile.SaveAs(path);
                        course.Image = imageName;
                    }
                    else
                    {
                        course.Image = OldImage;
                    }

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

    [HttpPost]
    public ActionResult DeleteCourse(int id)
    {
        try
        {
            var course = db.Courses.SingleOrDefault(c => c.CourseID == id);
            if (course == null)
            {
                return Json(new { success = false, message = "Course not found." });
            }

            bool hasEnrollment = db.Enrollments.Any(e => e.CourseID == id);
            if (hasEnrollment)
            {
                return Json(new { success = false, message = "Cannot delete! This course already has students enrolled. Try changing its status to Inactive instead." });
            }

            db.Courses.DeleteOnSubmit(course);
            db.SubmitChanges();

            return Json(new { success = true, message = "Course deleted successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Error: " + ex.Message });
        }
    }

    // --- BẮT ĐẦU PHẦN ĐỒNG BỘ GIỜ VIỆT NAM CHO TRANSACTION LOGIC ---

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
                                // Đồng bộ gán giờ VN (Nếu null thì lấy luôn giờ VN hiện tại)
                                PaymentDate = p.PaymentDate ?? GetVnNow(),
                                Progress = (double)(e.Progress ?? 0)
                            }).ToList();
        return View(transactions);
    }

    [HttpPost]
    public JsonResult ApprovePayment(int paymentId)
    {
        var payment = db.Payments.SingleOrDefault(p => p.PaymentID == paymentId);
        if (payment == null) return Json(new { success = false, message = "Không tìm thấy!" });

        payment.PaymentStatus = "Paid";
        payment.PaymentDate = GetVnNow(); // 🔥 ÉP LƯU THEO GIỜ VN (GMT+7)
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

        DateTime vnNow = GetVnNow();
        bool isPending = data.Payment.PaymentStatus == "Pending";

        bool canRefund = (data.Payment.PaymentStatus == "Paid" &&
                         data.Payment.PaymentDate.HasValue &&
                         (vnNow - data.Payment.PaymentDate.Value).TotalHours <= 24 &&
                         data.Enrollment.Progress < 5);

        if (!isPending && !canRefund)
            return Json(new { success = false, message = "Cannot cancel: Course is in progress or refund window closed." });

        // 🌟 CHỈ CẬP NHẬT TRẠNG THÁI, KHÔNG XÓA ENROLLMENT
        data.Payment.PaymentStatus = "Cancelled";
        data.Payment.PaymentDate = vnNow;

        db.SubmitChanges();
        return Json(new { success = true, message = "Cancelled successfully." });
    }

    [HttpPost]
    public JsonResult UndoPayment(int paymentId)
    {
        var payment = db.Payments.SingleOrDefault(p => p.PaymentID == paymentId);
        if (payment == null) return Json(new { success = false, message = "Transaction record not found." });

        DateTime vnNow = GetVnNow();

        if (payment.PaymentDate.HasValue && (vnNow - payment.PaymentDate.Value).TotalMinutes > 15)
        {
            return Json(new { success = false, message = "Undo window (15 minutes) has expired for this transaction." });
        }

        // Đưa trạng thái về lại Pending ngon lành cành đào vì liên kết Enrollment không bị đứt
        payment.PaymentStatus = "Pending";
        payment.PaymentDate = vnNow;
        db.SubmitChanges();

        return Json(new { success = true, message = "Transaction status has been restored to Pending." });
    }

    // Action xuất file thống kê CSV (UTF-8 mã hóa để mở Excel không lỗi font)
    public ActionResult ExportStatistics()
    {
        // 1. Thu thập lại toàn bộ số liệu giống y hệt ngoài Dashboard
        var totalCourses = db.Courses.Count();
        var totalStudents = db.Students.Count();
        var totalInstructors = db.Instructors.Count();
        var totalEnrollments = db.Enrollments.Count();
        var totalRevenue = db.Payments.Where(p => p.PaymentStatus == "Paid").Sum(p => (decimal?)p.Amount).GetValueOrDefault(0);

        var allReviews = db.Reviews.ToList();
        double avgRating = allReviews.Any() ? allReviews.Average(r => r.Rating) : 5.0;

        // 2. Tạo nội dung file CSV
        var sw = new StringWriter();

        // Viết tiêu đề cột
        sw.WriteLine("Metric Name,Statistic Value");

        // Viết dữ liệu dòng
        sw.WriteLine($"Total Courses,{totalCourses}");
        sw.WriteLine($"Total Students,{totalStudents}");
        sw.WriteLine($"Total Instructors,{totalInstructors}");
        sw.WriteLine($"Total Enrollments,{totalEnrollments}");
        sw.WriteLine($"Total Revenue (VND),{totalRevenue}");
        sw.WriteLine($"Average Course Rating,{avgRating.ToString("F1")}");
        sw.WriteLine($"Exported Date,{GetVnNow().ToString("dd/MM/yyyy HH:mm:ss")}");

        // 3. Ép kiểu mã hóa UTF-8 kèm BOM (Byte Order Mark) để Excel mở lên đọc được dấu tiếng Việt/ký tự đặc biệt
        var stringData = sw.ToString();
        var bytes = System.Text.Encoding.UTF8.GetBytes(stringData);
        var result = new byte[bytes.Length + 3];
        result[0] = 0xEF; // BOM định dạng UTF-8
        result[1] = 0xBB;
        result[2] = 0xBF;
        Array.Copy(bytes, 0, result, 3, bytes.Length);

        // 4. Trả file về cho trình duyệt tự động kích hoạt lệnh Download
        string fileName = "System_Statistics_" + GetVnNow().ToString("yyyyMMdd_HHmmss") + ".csv";
        return File(result, "text/csv", fileName);
    }

}