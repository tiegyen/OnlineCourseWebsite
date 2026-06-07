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
            // Chỗ này ní lấy từ bảng trung gian (Enrollment) dựa vào ID học viên hiện tại
            var myEnrolledCourses = db.Enrollments
                                     .Where(e => e.StudentID == student.StudentID)
                                     .Select(e => e.Course) // Bốc thẳng dữ liệu Model Course đi kèm ra
                                     .ToList();

            // 🌟 MẸO NHỎ (Tùy chọn): Nếu bảng Enrollment của ní có lưu sẵn cột Progress (Tiến độ % học), 
            // ní có thể quăng nguyên danh sách Enrollment sang View luôn. 
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
            if (checkEnroll == null) return RedirectToAction("MyCourses");

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

        // POST: Student/UpdateProfile
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

                // Cập nhật thông tin mới
                student.FullName = FullName;
                student.Phone = Phone;
                student.Address = Address;

                // 🌟 SỬA TẠI ĐÂY: Đổi SaveChanges thành SubmitChanges ghen ní!
                db.SubmitChanges();

                Session["StudentProfile"] = student;
                TempData["Success"] = "Profile updated successfully!";
            }

            return RedirectToAction("Profile");
        }

    }
}