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

    // POST: Xử lý cập nhật thông tin Instructor + Đổi Pass
    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult EditInstructor(Instructor model, string NewPassword)
    {
        if (ModelState.IsValid)
        {
            // 1. Cập nhật bảng thông tin cá nhân Instructor
            var instructor = db.Instructors.FirstOrDefault(i => i.InstructorID == model.InstructorID);
            if (instructor != null)
            {
                instructor.FullName = model.FullName;
                instructor.Phone = model.Phone;
                instructor.Address = model.Address;
                instructor.Bio = model.Bio;

                // 2. Kiểm tra nếu Admin có điền vào ô Mật khẩu mới
                if (!string.IsNullOrEmpty(NewPassword))
                {
                    var account = db.User_Accounts.FirstOrDefault(u => u.UserID == model.UserID);
                    if (account != null)
                    {
                        // Gán thẳng mật khẩu mới (Nếu hệ thống của ní có hàm băm MD5/SHA256 thì bọc nó vào ghen)
                        account.Password = NewPassword.Trim();
                    }
                }

                db.SubmitChanges(); // Thực thi lưu dữ liệu xuống SQL Server
                TempData["Success"] = "Instructor updated successfully!";
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

    // POST: Xử lý cập nhật thông tin Student + Đổi Pass
    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult EditStudent(Student model, string NewPassword)
    {
        if (ModelState.IsValid)
        {
            // 1. Cập nhật bảng thông tin cá nhân Student
            var student = db.Students.FirstOrDefault(s => s.StudentID == model.StudentID);
            if (student != null)
            {
                student.FullName = model.FullName;
                student.Phone = model.Phone;
                student.Address = model.Address;

                // 2. Kiểm tra nếu Admin có điền vào ô Mật khẩu mới
                if (!string.IsNullOrEmpty(NewPassword))
                {
                    var account = db.User_Accounts.FirstOrDefault(u => u.UserID == model.UserID);
                    if (account != null)
                    {
                        account.Password = NewPassword.Trim();
                    }
                }

                db.SubmitChanges(); // Thực thi lưu dữ liệu xuống SQL Server
                TempData["Success"] = "Student updated successfully!";
                return RedirectToAction("Users");
            }
        }
        return View(model);
    }

    // ==================== TẠO MỚI INSTRUCTOR ====================

    // 1. GET: Hiển thị form điền thông tin thêm Instructor
    public ActionResult CreateInstructor()
    {
        return View();
    }

    // 2. POST: Đón dữ liệu form gửi về và insert vào Database
    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult CreateInstructor(string Email, string Password, string FullName, string Phone, string Address, string Bio)
    {
        try
        {
            // Kiểm tra dữ liệu đầu vào cơ bản
            if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Password) || string.IsNullOrEmpty(FullName))
            {
                ModelState.AddModelError("", "Email, Password and Full Name are required.");
                return View();
            }

            // Check trùng Email trong hệ thống
            bool isEmailExist = db.User_Accounts.Any(u => u.Email.ToLower() == Email.Trim().ToLower());
            if (isEmailExist)
            {
                ModelState.AddModelError("", "This email address is already registered!");
                return View();
            }

            // BƯỚC A: Thêm tài khoản vào User_Accounts trước
            User_Account newAccount = new User_Account
            {
                Email = Email.Trim(),
                Password = Password.Trim(), // Nếu ní có hàm băm mật khẩu (VD: MD5, SHA256) thì băm nó ở đây nha
                Role = "Instructor",
                Status = "Active"
            };

            db.User_Accounts.InsertOnSubmit(newAccount);
            db.SubmitChanges(); // Chốt hạ đợt 1 để SQL cấp phát tự động mã UserID cho newAccount

            // BƯỚC B: Dùng mã UserID vừa sinh ra để liên kết tạo Instructor
            Instructor newInstructor = new Instructor
            {
                UserID = newAccount.UserID, // Khóa ngoại đồng bộ link sang tài khoản vừa tạo
                FullName = FullName.Trim(),
                Phone = Phone?.Trim(),
                Address = Address?.Trim(),
                Bio = Bio?.Trim()
            };

            db.Instructors.InsertOnSubmit(newInstructor);
            db.SubmitChanges(); // Chốt hạ đợt 2 lưu thông tin cá nhân

            TempData["Success"] = "Instructor account created successfully!";
            return RedirectToAction("Users");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "Database error occurred: " + ex.Message);
            return View();
        }
    }


    

    
    // POST: Admin/CreateCourse
    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult CreateCourse(Course model, HttpPostedFileBase ImageFile)
    {
        try
        {
            // 1. Kiểm tra dữ liệu đầu vào cơ bản
            if (model == null || string.IsNullOrEmpty(model.CourseName))
            {
                TempData["Error"] = "Tên khóa học không được để trống!";
                return RedirectToAction("Courses");
            }

            // 2. Tạo đối tượng mới để map chuẩn
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

            // 3. Xử lý lưu ảnh (Đã đồng bộ về Content/images/)
            if (ImageFile != null && ImageFile.ContentLength > 0)
            {
                string extension = Path.GetExtension(ImageFile.FileName);
                string imageName = "course_" + Guid.NewGuid().ToString().Substring(0, 8) + extension;

                // Lấy đường dẫn thư mục cha
                string folderPath = Server.MapPath("~/Content/images/");

                // Khống chế lỗi: Nếu chưa có thư mục "images" thì tự tạo luôn cho đỡ lỗi
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
                newCourse.Image = "default.png"; // Hoặc để null tùy DB của ní
            }

            // 4. Tiến hành nạp và Submit
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
            // Vạch trần tất cả các lỗi ngầm ở đây 👇
            System.Diagnostics.Debug.WriteLine("❌ LỖI CREATE COURSE CHI TIẾT: " + ex.ToString());

            // Nếu có lỗi inner (lỗi sâu bên trong SQL gửi về)
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine("❌ LỖI GỐC TỪ SQL: " + ex.InnerException.Message);
            }

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

    // POST: Admin/DeleteCourse
    [HttpPost]
    public ActionResult DeleteCourse(int id)
    {
        try
        {
            // Tìm khoá học cần xoá
            var course = db.Courses.SingleOrDefault(c => c.CourseID == id);
            if (course == null)
            {
                return Json(new { success = false, message = "Course not found." });
            }

            // KIỂM TRA RÀNG BUỘC: Nếu khóa học đã có người đăng ký học (Enrollment)
            bool hasEnrollment = db.Enrollments.Any(e => e.CourseID == id);
            if (hasEnrollment)
            {
                // Giải pháp an toàn: Không cho xoá, bắt đổi Status sang "Inactive" hoặc báo lỗi
                return Json(new { success = false, message = "Cannot delete! This course already has students enrolled. Try changing its status to Inactive instead." });
            }

            // Nếu sạch sẽ không vướng khoá ngoại, tiến hành xoá
            db.Courses.DeleteOnSubmit(course);
            db.SubmitChanges();

            return Json(new { success = true, message = "Course deleted successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Error: " + ex.Message });
        }
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



    


