using OnlineCourseWebsite.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using PagedList;
namespace OnlineCourseWebsite.Controllers
{
    public class CourseController : Controller
    {
        dbOnlineCourseDataContext db = new dbOnlineCourseDataContext();
        // GET: Course
        public ActionResult Course(int? page, int? currentCategory, string searchString)
        {
            int pageSize = 4;
            int pageNumber = (page ?? 1); // nếu page bị null thì mặc định lấy page 1 

            var coursesQuery = db.Courses.AsQueryable(); // lấy toàn bộ danh sách dưới dạng Queryable để lọc

            if (!string.IsNullOrEmpty(searchString))
            {
                searchString = searchString.Trim();
                coursesQuery = coursesQuery.Where(c => c.CourseName.Contains(searchString)
                || (c.Instructor != null && c.Instructor.FullName.Contains(searchString)));
            }


            if (currentCategory.HasValue && currentCategory.Value > 0)
            {
                coursesQuery = coursesQuery.Where(c => c.CategoryID == currentCategory.Value);
            }

            var allCourses = coursesQuery.OrderBy(c => c.CourseID).ToList();  

            ViewBag.Categories = db.Categories.ToList();

            ViewBag.CurrentCategory = currentCategory;

            ViewBag.CurrentSearch = searchString;

            return View(allCourses.ToPagedList(pageNumber,pageSize));
        }



        // GET: Course/Details/5
        public ActionResult Details(int id)
        {
            var course = db.Courses.SingleOrDefault(c => c.CourseID == id);
            if (course == null) return HttpNotFound();

            // Giả định ní load danh sách reviews
            ViewBag.Reviews = db.Reviews.Where(r => r.CourseID == id).ToList();

            // KIỂM TRA TRẠNG THÁI THANH TOÁN ĐỂ KHÓA/MỞ NÚT VÀO HỌC
            var studentSession = Session["StudentProfile"] as OnlineCourseWebsite.Models.Student;
            if (studentSession != null)
            {
                // 1. Check xem đã tồn tại dòng Enrollment chưa
                var enroll = db.Enrollments.FirstOrDefault(e => e.StudentID == studentSession.StudentID && e.CourseID == id);
                if (enroll != null)
                {
                    ViewBag.IsEnrolled = true;
                    ViewBag.EnrollmentID = enroll.EnrollmentID;

                    // 2. 🌟 BẢO MẬT THÊM CHỖ NÀY: Tìm hóa đơn đi kèm xem đã PAID chưa
                    var payment = db.Payments.FirstOrDefault(p => p.EnrollmentID == enroll.EnrollmentID);
                    if (payment != null && payment.PaymentStatus == "Paid")
                    {
                        ViewBag.IsPaid = true; // Đã trả tiền -> Được mở khóa Classroom
                    }
                    else
                    {
                        ViewBag.IsPaid = false; // Chưa trả tiền hoặc bị Cancelled -> Bị chặn lại
                    }
                }
                else
                {
                    ViewBag.IsEnrolled = false;
                    ViewBag.IsPaid = false;
                }
            }
            else
            {
                ViewBag.IsEnrolled = false;
                ViewBag.IsPaid = false;
            }

            return View(course);
        }

        // 1. Trang xác nhận đơn hàng
        public ActionResult Enroll(int id)
        {
            // Lấy thông tin khóa học và truyền sang view EnrollPage
            var course = db.Courses.SingleOrDefault(c => c.CourseID == id);
            return View("EnrollPage", course);
        }

        // 2. Sau khi xác nhận xong, dẫn sang trang Thanh toán (QR Code)
        public ActionResult PaymentDetails(int paymentId)
        {
            var payment = db.Payments.SingleOrDefault(p => p.PaymentID == paymentId);
            if (payment == null) return HttpNotFound();

            // Truyền dữ liệu qua View bằng ViewBag (để view QR cũ hoạt động được)
            ViewBag.CourseName = payment.Enrollment.Course.CourseName;
            ViewBag.Price = payment.Amount;

            return View("PaymentDetails", payment); // Tên file .cshtml chứa QR Code
        }

        // POST: Course/ConfirmEnroll
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public ActionResult ConfirmEnroll(int courseID)
        //{
        //    var studentSession = Session["StudentProfile"] as OnlineCourseWebsite.Models.Student;
        //    if (studentSession == null) return RedirectToAction("Login", "User");

        //    var course = db.Courses.SingleOrDefault(c => c.CourseID == courseID);
        //    if (course == null) return HttpNotFound();

        //    // BƯỚC 1: TẠO ENROLLMENT
        //    var newEnroll = new Enrollment
        //    {
        //        StudentID = studentSession.StudentID,
        //        CourseID = courseID,
        //        EnrollmentDate = DateTime.Now,
        //        Progress = 0,
        //        LearningStatus = "Learning"
        //    };
        //    db.Enrollments.InsertOnSubmit(newEnroll);
        //    db.SubmitChanges();

        //    // BƯỚC 2: TẠO PAYMENT
        //    var newPayment = new Payment
        //    {
        //        EnrollmentID = newEnroll.EnrollmentID,
        //        Amount = course.Price.GetValueOrDefault(0),
        //        PaymentDate = DateTime.Now,
        //        PaymentMethod = "Bank Transfer",
        //        PaymentStatus = "Pending"
        //    };
        //    db.Payments.InsertOnSubmit(newPayment);
        //    db.SubmitChanges();

        //    return RedirectToAction("Payment", "Student"); // Đá về trang lịch sử để thấy hóa đơn vừa tạo
        //}

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmEnroll(int courseID)
        {
            var studentSession = Session["StudentProfile"] as OnlineCourseWebsite.Models.Student;
            if (studentSession == null) return RedirectToAction("Login", "User");

            var course = db.Courses.SingleOrDefault(c => c.CourseID == courseID);
            if (course == null) return HttpNotFound();

            // BƯỚC 1: TẠO ENROLLMENT
            var newEnroll = new Enrollment
            {
                StudentID = studentSession.StudentID,
                CourseID = courseID,
                EnrollmentDate = DateTime.Now,
                Progress = 0,
                LearningStatus = "Learning"
            };
            db.Enrollments.InsertOnSubmit(newEnroll);
            db.SubmitChanges();

            // BƯỚC 2: TẠO PAYMENT
            var newPayment = new Payment
            {
                EnrollmentID = newEnroll.EnrollmentID,
                Amount = course.Price.GetValueOrDefault(0),
                PaymentDate = DateTime.Now,
                PaymentMethod = "Bank Transfer",
                PaymentStatus = "Pending"
            };
            db.Payments.InsertOnSubmit(newPayment);
            db.SubmitChanges();

            // 🌟 SỬA Ở ĐÂY: Đá về trang chi tiết thanh toán có mã QR
            // Ní thay "PaymentDetails" bằng tên View chứa QR Code của ní
            return RedirectToAction("PaymentDetails", "Course", new { paymentId = newPayment.PaymentID });
        }


        // GET: Course/EnrollPage?enrollmentId=... HOẶC ?paymentId=...
        public ActionResult EnrollPage(int? enrollmentId, int? paymentId) // 🌟 Nhận cả 2 đầu 
        {
            var studentSession = Session["StudentProfile"] as OnlineCourseWebsite.Models.Student;
            if (studentSession == null) return RedirectToAction("Login", "User");

            OnlineCourseWebsite.Models.Payment payment = null;

            // 1. Nếu đi từ nút "Pay Now" ở trang lịch sử (Có paymentId)
            if (paymentId != null)
            {
                payment = db.Payments.SingleOrDefault(p => p.PaymentID == paymentId.Value);
            }
            // 2. Nếu đi từ nút "Enroll Now" ở trang chi tiết (Có enrollmentId)
            else if (enrollmentId != null)
            {
                payment = db.Payments.FirstOrDefault(p => p.EnrollmentID == enrollmentId.Value);
            }

            // Nếu không tìm thấy hóa đơn nào hết thì báo lỗi bảo vệ hệ thống
            if (payment == null)
            {
                TempData["Error"] = "Payment session not found or invalid!";
                return RedirectToAction("Index", "Home");
            }

            // Bốc thông tin khóa học tương ứng dựa trên hóa đơn tìm được để ném lên ViewBag
            var enroll = db.Enrollments.SingleOrDefault(e => e.EnrollmentID == payment.EnrollmentID);
            if (enroll != null)
            {
                ViewBag.CourseName = enroll.Course.CourseName;
                ViewBag.Price = enroll.Course.Price;
            }

            return View(payment); // Trả về đúng Model Payment cho View EnrollPage 
        }

        // POST: Course/CancelOrder
        [HttpPost]
        public ActionResult CancelOrder(int paymentId)
        {
            var studentSession = Session["StudentProfile"] as OnlineCourseWebsite.Models.Student;
            if (studentSession == null) return RedirectToAction("Login", "User");

            // 1. Tìm hóa đơn tương ứng dưới DB
            var payment = db.Payments.SingleOrDefault(p => p.PaymentID == paymentId);

            if (payment != null && payment.PaymentStatus == "Pending")
            {
                // 2. Chuyển trạng thái hóa đơn thành Cancelled để lưu vết lịch sử
                payment.PaymentStatus = "Cancelled";

                // 3. XÓA dòng Enrollment tương ứng để giải phóng CONSTRAINT UNIQUE (StudentID, CourseID)
                // Điều này giúp học viên sau này muốn mua lại khóa học đó thì vẫn bấm "Enroll Now" được ghen ní!
                var enroll = db.Enrollments.SingleOrDefault(e => e.EnrollmentID == payment.EnrollmentID);
                if (enroll != null)
                {
                    db.Enrollments.DeleteOnSubmit(enroll); // Xóa enrollment tạm thời này đi
                }

                db.SubmitChanges(); // Lưu tổng thể xuống SQL Server
                TempData["Success"] = "Order cancelled successfully!";
            }

            // Xử lý xong đá ngược về lại trang lịch sử mua hàng
            return RedirectToAction("Payment", "Student");
        }

        //// POST: Course/SubmitManualPayment
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public ActionResult SubmitManualPayment(int paymentId)
        //{
        //    var studentSession = Session["StudentProfile"] as Student;
        //    if (studentSession == null) return RedirectToAction("Login", "User");

        //    // 1. Tìm hóa đơn
        //    var payment = db.Payments.SingleOrDefault(p => p.PaymentID == paymentId);
        //    if (payment == null) return HttpNotFound();

        //    if (payment.PaymentStatus == "Pending")
        //    {
        //        // 2. Chuyển trạng thái thành "Awaiting" (Chờ admin duyệt) 
        //        // Muốn nhanh cho đồ án mượt thì chuyển thẳng thành "Paid"
        //        payment.PaymentStatus = "Paid"; 
        //        payment.PaymentMethod = "Bank Transfer";
        //        payment.PaymentDate = DateTime.Now;

        //        db.SubmitChanges();
        //    }

        //    TempData["Success"] = "Payment verification submitted! Your course is now unlocked.";
        //    return RedirectToAction("MyCourses", "Student");
        //}

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitManualPayment(int paymentId)
        {
            var studentSession = Session["StudentProfile"] as OnlineCourseWebsite.Models.Student;
            if (studentSession == null) return RedirectToAction("Login", "User");

            var payment = db.Payments.SingleOrDefault(p => p.PaymentID == paymentId);
            if (payment == null) return HttpNotFound();

            if (payment.PaymentStatus == "Pending")
            {
                // Không dùng "Awaiting" nữa vì SQL chặn
                // Ní có thể giữ nguyên "Pending" hoặc đổi thành trạng thái hợp lệ dưới DB của ní
                payment.PaymentStatus = "Pending";
                payment.PaymentMethod = "Bank Transfer";
                payment.PaymentDate = DateTime.Now;

                db.SubmitChanges();
            }

            // Đổi lại câu thông báo cho đúng ngữ cảnh nè
            TempData["Success"] = "Payment submitted successfully! Please wait for Admin approval.";
            TempData.Keep();

            return RedirectToAction("MyCourses", "Student");
        }





    }
}