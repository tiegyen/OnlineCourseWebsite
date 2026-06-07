using OnlineCourseWebsite.Models; // Ní nhớ check đúng namespace Models của hai ní nha
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace OnlineCourseWebsite.Controllers
{
    public class StudentController : Controller
    {
        private dbOnlineCourseDataContext db = new dbOnlineCourseDataContext(); // Thay bằng tên DbContext của ní

        // GET: Student/MyCourses
        public ActionResult MyCourses()
        {

            
            // 1. Kiểm tra xem học viên đã đăng nhập chưa
            var student = Session["StudentProfile"] as Student;
            if (student == null)
            {
                // Nếu chưa đăng nhập, sút ngay về trang Đăng nhập cho an toàn
                return RedirectToAction("Login", "User");
            }

            // 2. Lấy danh sách các khóa học mà học viên này đã đăng ký/mua thành công
            
            // 2. Lấy danh sách các khóa học mà học viên này đã THANH TOÁN THÀNH CÔNG (Paid)
            // 🌟 ĐÃ SỬA: Dùng Any để kiểm tra bảng Payments đi kèm phải có trạng thái là "Paid"
            var myEnrolledCourses = db.Enrollments
                                     .Where(e => e.StudentID == student.StudentID &&
                                                 db.Payments.Any(p => p.EnrollmentID == e.EnrollmentID && p.PaymentStatus == "Paid"))
                                     .Select(e => e.Course)
                                     .ToList();

            // 🌟 MẸO NHỎ (Tùy chọn): Nếu bảng Enrollment có lưu sẵn cột Progress (Tiến độ % học), 
            // có thể quăng nguyên danh sách Enrollment sang View luôn. 
            // Ở đây Gen truyền List<Course> làm Model chính để khớp với sườn giao diện của ní ghen.

            return View(myEnrolledCourses);
        }

        // GET: Student/Classroom/5
        public ActionResult Classroom(int? id)
        {
            if (id == null)
            {
                return RedirectToAction("MyCourses");
            }

            var student = Session["StudentProfile"] as Student;
            if (student == null) return RedirectToAction("Login", "User");

            // Đổi tất cả những chỗ check id thành id.Value vì lúc này nó là Nullable ghen ní
            var checkEnroll = db.Enrollments.FirstOrDefault(e => e.StudentID == student.StudentID && e.CourseID == id.Value);

            // 🌟 THẮT CHẶT AN NINH: Nếu không có đăng ký HOẶC hóa đơn chưa "Paid" thì sút bay về trang MyCourses liền!
            if (checkEnroll == null || !db.Payments.Any(p => p.EnrollmentID == checkEnroll.EnrollmentID && p.PaymentStatus == "Paid"))
            {
                return RedirectToAction("MyCourses");
            }

            var course = db.Courses.SingleOrDefault(c => c.CourseID == id.Value);
            if (course == null) return HttpNotFound();

            var lessons = db.Lessons
                            .Where(l => l.CourseID == id.Value)
                            .OrderBy(l => l.LessonOrder)
                            .ToList();
            ViewBag.Lessons = lessons;

            Lesson activeLesson = null;
            if (lessons.Any())
            {
                activeLesson = lessons.FirstOrDefault();

                // 🌟 BỊP THỦ CÔNG TẠI ĐÂY: Tự vào bảng CourseMaterial bốc tài liệu của riêng bài học này lên
                var materials = db.CourseMaterials
                                  .Where(m => m.LessonID == activeLesson.LessonID)
                                  .ToList();
                ViewBag.ActiveMaterials = materials; // Quăng đống tài liệu này vào một ViewBag riêng
            }

            ViewBag.ActiveLesson = activeLesson;

            return View(course);
        }

        // GET: Student/Profile
        public ActionResult Profile()
        {
            // 1. Kiểm tra đăng nhập
            var studentSession = Session["StudentProfile"] as Student;
            if (studentSession == null)
            {
                return RedirectToAction("Login", "User");
            }

            // 2. Bốc dữ liệu mới nhất từ DB lên để tránh trường hợp Session bị cũ
            var student = db.Students.SingleOrDefault(s => s.StudentID == studentSession.StudentID);
            if (student == null)
            {
                return HttpNotFound();
            }

            return View(student);
        }

        

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateProfile(string FullName, string Phone, string Address)
        {
            var studentSession = Session["StudentProfile"] as Student;
            if (studentSession == null) return RedirectToAction("Login", "User");

            var student = db.Students.SingleOrDefault(s => s.StudentID == studentSession.StudentID);
            if (student != null)
            {
                if (string.IsNullOrEmpty(FullName))
                {
                    TempData["Error"] = "Full Name cannot be empty!";
                    return RedirectToAction("Profile");
                }

                student.FullName = FullName;
                student.Phone = Phone;
                student.Address = Address;

                db.SubmitChanges();
                Session["StudentProfile"] = student; // Cập nhật Session hiển thị tên mới

                TempData["Success"] = "Profile updated successfully!";
            }
            return RedirectToAction("Profile");
        }

        // 2. XỬ LÝ UPLOAD HÌNH ẢNH AVATAR
        [HttpPost]
        public ActionResult UploadAvatar(HttpPostedFileBase avatarFile)
        {
            var studentSession = Session["StudentProfile"] as Student;
            if (studentSession == null) return Json(new { success = false, message = "Session expired." });

            if (avatarFile != null && avatarFile.ContentLength > 0)
            {
                try
                {
                    // Kiểm tra đuôi file có phải là hình ảnh không
                    string ext = Path.GetExtension(avatarFile.FileName).ToLower();
                    if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".gif")
                    {
                        return Json(new { success = false, message = "Only JPG, JPEG, PNG, or GIF files are allowed." });
                    }

                    // Tạo tên file duy nhất theo ID của học viên để không bị đè hình trùng tên
                    string fileName = "avatar_student_" + studentSession.StudentID + ext;

                    // Đường dẫn thư mục lưu file trên Server (Ví dụ: ~/Content/images/)
                    string folderPath = Server.MapPath("~/Content/images/");

                    // Nếu chưa có thư mục thì tự tạo luôn cho an toàn
                    if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                    string fullPath = Path.Combine(folderPath, fileName);

                    // Lưu file hình vật lý vào thư mục dự án
                    avatarFile.SaveAs(fullPath);

                    // Cập nhật đường dẫn hình mới vào Database (cột Avatar của hai ní)
                    var student = db.Students.SingleOrDefault(s => s.StudentID == studentSession.StudentID);
                    student.Avatar = "~/Content/images/" + fileName;

                    db.SubmitChanges();
                    Session["StudentProfile"] = student; // Cập nhật lại Session hình ảnh mới

                    return Json(new { success = true, imgUrl = Url.Content(student.Avatar) });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = "Error: " + ex.Message });
                }
            }
            return Json(new { success = false, message = "No file selected." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangePassword(string CurrentPassword, string NewPassword, string ConfirmPassword)
        {
            var studentSession = Session["StudentProfile"] as Student;
            if (studentSession == null) return RedirectToAction("Login", "User");

            // Lấy UserID từ thông tin Student hiện tại
            var userId = studentSession.UserID;

            // Truy vấn vào bảng User_Account để đổi mật khẩu
            var userAcc = db.User_Accounts.SingleOrDefault(u => u.UserID == userId);

            if (userAcc != null)
            {
                // So sánh với Password trong User_Account
                if (userAcc.Password.Trim() != CurrentPassword.Trim())
                {
                    TempData["Error"] = "Current password is incorrect!";
                    return RedirectToAction("Profile");
                }

                // Validate mật khẩu mới...
                if (NewPassword != ConfirmPassword)
                {
                    TempData["Error"] = "New passwords do not match!";
                    return RedirectToAction("Profile");
                }

                // Cập nhật và Submit
                userAcc.Password = NewPassword;
                db.SubmitChanges(); 

                TempData["Success"] = "Password changed successfully!";
            }
            return RedirectToAction("Profile");
        }

        public ActionResult Payment()
        {
            var studentSession = Session["StudentProfile"] as OnlineCourseWebsite.Models.Student;
            if(studentSession == null)
            {
                return RedirectToAction("Login", "User");
            }

            var paymentHistory = (from p in db.Payments
                                  join e in db.Enrollments on p.EnrollmentID equals e.EnrollmentID
                                  join c in db.Courses on e.CourseID equals c.CourseID
                                  where e.StudentID == studentSession.StudentID
                                  orderby p.PaymentDate descending
                                  select new OnlineCourseWebsite.Models.ViewModel.PaymentRow
                                  {
                                      PaymentID = p.PaymentID,
                                      CourseName = c.CourseName,
                                      Amount = p.Amount,
                                      PaymentMethod = p.PaymentMethod,
                                      PaymentStatus = p.PaymentStatus,
                                      PaymentDate = p.PaymentDate
                                  }).ToList();
            return View(paymentHistory);
        }



    }
}