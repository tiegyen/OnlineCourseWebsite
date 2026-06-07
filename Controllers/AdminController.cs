using OnlineCourseWebsite.Models;
using System.Linq;
using System.Web.Mvc;

public class AdminController : Controller
{
    dbOnlineCourseDataContext db = new dbOnlineCourseDataContext();

    // Trang chủ Admin (Dashboard)
    public ActionResult Dashboard()
    {
        // Đếm số lượng thực tế dưới DB
        ViewBag.TotalCourses = db.Courses.Count();
        ViewBag.TotalStudents = db.Students.Count();
        ViewBag.TotalInstructors = db.Instructors.Count();
        ViewBag.TotalEnrollments = db.Enrollments.Count();

        // Tính tổng doanh thu (chỉ tính những đơn Paid)
        
        ViewBag.TotalRevenue = db.Payments.Where(p => p.PaymentStatus == "Paid").Sum(p => (decimal?)p.Amount) ?? 0m;

        return View();
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
}