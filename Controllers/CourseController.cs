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
            int pageNumber = (page ?? 1);

            var coursesQuery = db.Courses.AsQueryable();

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

            // 🌟 TỐI ƯU: Sắp xếp trước, KHÔNG dùng .ToList() ở đây để SQL Server tự phân trang tối ưu hiệu năng
            var pagedCourses = coursesQuery.OrderBy(c => c.CourseID).ToPagedList(pageNumber, pageSize);

            ViewBag.Categories = db.Categories.ToList();
            ViewBag.CurrentCategory = currentCategory;
            ViewBag.CurrentSearch = searchString;

            // Lấy danh sách ID các khóa học đã THANH TOÁN THÀNH CÔNG (Paid)
            List<int> purchasedCourseIDs = new List<int>();
            // Lấy danh sách ID các khóa học ĐANG CHỜ DUYỆT (Pending)
            List<int> pendingCourseIDs = new List<int>();

            var studentSession = Session["StudentProfile"] as OnlineCourseWebsite.Models.Student;
            if (studentSession != null)
            {
                // 1. Khóa học đã Paid
                purchasedCourseIDs = db.Enrollments
                    .Where(e => e.StudentID == studentSession.StudentID &&
                                db.Payments.Any(p => p.EnrollmentID == e.EnrollmentID && p.PaymentStatus == "Paid"))
                    .Select(e => e.CourseID)
                    .ToList();

                // 2. Khóa học đang Pending (Chờ Admin duyệt)
                pendingCourseIDs = db.Enrollments
                    .Where(e => e.StudentID == studentSession.StudentID &&
                                db.Payments.Any(p => p.EnrollmentID == e.EnrollmentID && p.PaymentStatus == "Pending"))
                    .Select(e => e.CourseID)
                    .ToList();

                // 💡 Mẹo nhỏ: Đơn "Cancelled" KHÔNG lọc vào đây, nên View sẽ tự động coi như chưa mua và hiện lại nút "Enroll Now" ngon lành!
            }

            ViewBag.PurchasedCourseIDs = purchasedCourseIDs;
            ViewBag.PendingCourseIDs = pendingCourseIDs; // Gửi list pending sang View

            return View(pagedCourses);
        }

        // 2. Hàm chi tiết khóa học (Details)
        public ActionResult Details(int id)
        {
            var course = db.Courses.SingleOrDefault(c => c.CourseID == id);
            if (course == null) return HttpNotFound();

            // 🌟 CHỐT CHẶN BẢO MẬT BẰNG LINQ: 
            // Nếu ai đó cố tình gõ URL của khóa học mang trạng thái "Coming Soon", đá văng ra lại trang danh sách!
            if (course.CourseStatus != null && course.CourseStatus.StatusName != null
                && course.CourseStatus.StatusName.Trim().ToLower() == "coming soon")
            {
                TempData["ErrorMessage"] = "Khóa học này sắp ra mắt, bạn chưa thể xem chi tiết được ghen!";
                return RedirectToAction("Course");
            }

            ViewBag.Reviews = db.Reviews.Where(r => r.CourseID == id).ToList();

            var studentSession = Session["StudentProfile"] as OnlineCourseWebsite.Models.Student;
            if (studentSession != null)
            {
                var enroll = db.Enrollments.FirstOrDefault(e => e.StudentID == studentSession.StudentID && e.CourseID == id);
                if (enroll != null)
                {
                    ViewBag.IsEnrolled = true;
                    ViewBag.EnrollmentID = enroll.EnrollmentID;

                    var payment = db.Payments.FirstOrDefault(p => p.EnrollmentID == enroll.EnrollmentID);
                    if (payment != null && payment.PaymentStatus == "Paid")
                    {
                        ViewBag.IsPaid = true;
                    }
                    else
                    {
                        ViewBag.IsPaid = false;
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
        // Action hiển thị trang xác nhận (EnrollPage)
        // Action hiển thị trang xác nhận (EnrollPage)
        [HttpGet]
        public ActionResult Enroll(int id)
        {
            var studentSession = Session["StudentProfile"] as OnlineCourseWebsite.Models.Student;
            if (studentSession == null) return RedirectToAction("Login", "User");

            var course = db.Courses.SingleOrDefault(c => c.CourseID == id);
            if (course == null) return HttpNotFound();

            // Kiểm tra xem đã từng có Enrollment nào chưa
            var existingEnroll = db.Enrollments.FirstOrDefault(e => e.StudentID == studentSession.StudentID && e.CourseID == id);
            if (existingEnroll != null)
            {
                var associatedPayment = db.Payments.SingleOrDefault(p => p.EnrollmentID == existingEnroll.EnrollmentID);
                if (associatedPayment != null)
                {
                    if (associatedPayment.PaymentStatus == "Paid")
                    {
                        TempData["AlertMessage"] = "You have already purchased this course!";
                        return RedirectToAction("Course");
                    }
                    if (associatedPayment.PaymentStatus == "Pending")
                    {
                        TempData["AlertMessage"] = "This course is awaiting Admin approval.";
                        return RedirectToAction("Course");
                    }
                }
            }

            // 🌟 SỬA DÒNG NÀY: Chỉ định rõ ràng tên View là "EnrollPage"
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



        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public ActionResult ConfirmEnroll(int courseID)
        //{
        //    var studentSession = Session["StudentProfile"] as OnlineCourseWebsite.Models.Student;
        //    if (studentSession == null) return RedirectToAction("Login", "User");

        //    var course = db.Courses.SingleOrDefault(c => c.CourseID == courseID);
        //    if (course == null) return HttpNotFound();

        //    // CHỐT CHẶN 1: Kiểm tra xem đã đăng ký và THANH TOÁN THÀNH CÔNG (Paid) chưa
        //    var isAlreadyBought = db.Enrollments.Any(e => e.StudentID == studentSession.StudentID && e.CourseID == courseID &&
        //                          db.Payments.Any(p => p.EnrollmentID == e.EnrollmentID && p.PaymentStatus == "Paid"));

        //    if (isAlreadyBought)
        //    {
        //        TempData["AlertMessage"] = "You have already purchased and completed payment for this course! Please access it via your Classroom.";
        //        return RedirectToAction("Course");
        //    }

        //    // 🌟 CHỐT CHẶN 2 (THÊM MỚI): Kiểm tra xem khóa học có đang CHỜ DUYỆT (Pending) không
        //    var isPendingApproval = db.Enrollments.Any(e => e.StudentID == studentSession.StudentID && e.CourseID == courseID &&
        //                            db.Payments.Any(p => p.EnrollmentID == e.EnrollmentID && p.PaymentStatus == "Pending"));

        //    if (isPendingApproval)
        //    {
        //        // Gửi thông báo đang chờ duyệt
        //        TempData["AlertMessage"] = "This course is already in your cart and awaiting Admin approval. Please wait or check your Payment History!";
        //        return RedirectToAction("Course");
        //    }

        //    // BƯỚC 1: TẠO ENROLLMENT (Nếu không dính chốt chặn nào thì mới cho tạo mới như cũ)
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

        //    return RedirectToAction("PaymentDetails", "Course", new { paymentId = newPayment.PaymentID });
        //}

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmEnroll(int courseID)
        {
            var studentSession = Session["StudentProfile"] as OnlineCourseWebsite.Models.Student;
            if (studentSession == null) return RedirectToAction("Login", "User");

            var course = db.Courses.SingleOrDefault(c => c.CourseID == courseID);
            if (course == null) return HttpNotFound();

            // 1. Kiểm tra đơn hàng cũ (nếu có) của học viên với khóa học này
            var existingEnrollment = db.Enrollments.FirstOrDefault(e => e.StudentID == studentSession.StudentID && e.CourseID == courseID);

            if (existingEnrollment != null)
            {
                // Tìm hóa đơn Payment tương ứng của dòng đăng ký này
                var associatedPayment = db.Payments.SingleOrDefault(p => p.EnrollmentID == existingEnrollment.EnrollmentID);

                if (associatedPayment != null)
                {
                    // THÀNH CÔNG (Paid): Chặn không cho mua nữa
                    if (associatedPayment.PaymentStatus == "Paid")
                    {
                        TempData["AlertMessage"] = "You have already purchased this course! Please access it via your Classroom.";
                        return RedirectToAction("Course");
                    }

                    // CHỜ DUYỆT (Pending): Chặn bảo bấm lộn
                    if (associatedPayment.PaymentStatus == "Pending")
                    {
                        TempData["AlertMessage"] = "This course is awaiting Admin approval. Please wait or check your Payment History!";
                        return RedirectToAction("Course");
                    }

                    // ĐÃ HỦY (Cancelled) -> HỌC VIÊN MUỐN MUA LẠI:
                    if (associatedPayment.PaymentStatus == "Cancelled")
                    {
                        // 🌟 TÁI SỬ DỤNG ENROLLMENT CŨ: Chỉ cần reset dòng Payment cũ này về lại Pending
                        // Không thèm gọi lệnh Insert bảng Enrollment nữa => KHÔNG BAO GIỜ BỊ TRÙNG KEY!
                        associatedPayment.PaymentStatus = "Pending";
                        associatedPayment.PaymentDate = DateTime.Now;
                        associatedPayment.Amount = course.Price.GetValueOrDefault(0);
                        associatedPayment.PaymentMethod = "Bank Transfer";

                        // Reset lại tiến độ nếu cần
                        existingEnrollment.EnrollmentDate = DateTime.Now;
                        existingEnrollment.Progress = 0;

                        db.SubmitChanges();

                        return RedirectToAction("PaymentDetails", "Course", new { paymentId = associatedPayment.PaymentID });
                    }
                }
            }

            // 2. TRƯỜNG HỢP NÀY LÀ MUA LẦN ĐẦU TIÊN (Chưa từng có dòng Enrollment nào dưới DB)
            // Tiến hành tạo mới từ đầu như bình thường
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
                // Điều này giúp học viên sau này muốn mua lại khóa học đó thì vẫn bấm "Enroll Now" được
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
                // Ní có thể giữ nguyên "Pending" hoặc đổi thành trạng thái hợp lệ dưới DB
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