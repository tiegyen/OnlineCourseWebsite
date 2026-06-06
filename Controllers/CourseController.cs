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
            // 1. Lấy thông tin chi tiết của khóa học theo ID
            var course = db.Courses.SingleOrDefault(c => c.CourseID == id);
            if (course == null)
            {
                return HttpNotFound();
            }

            // 🌟 2. ĐÃ BỔ SUNG LẠI: Bốc danh sách Reviews của khóa học này từ DB lên
            // (Nhớ dùng .ToList() để View có thể duyệt danh sách ghen ní)
            var reviews = db.Reviews.Where(r => r.CourseID == id).ToList();
            ViewBag.Reviews = reviews;

            // 3. Xử lý Logic kiểm tra xem học viên hiện tại đã mua khóa học này chưa
            bool isEnrolled = false;
            var student = Session["StudentProfile"] as OnlineCourseWebsite.Models.Student;
            if (student != null)
            {
                // Kiểm tra xem cặp (StudentID, CourseID) đã tồn tại trong bảng đăng ký chưa
                // Ní nhớ đổi tên "Enrollments" thành đúng tên bảng lưu lịch sử mua của hai ní nha!
                var checkEnroll = db.Enrollments.SingleOrDefault(e => e.StudentID == student.StudentID && e.CourseID == id);
                if (checkEnroll != null)
                {
                    isEnrolled = true; // Đã mua rồi
                }
            }
            ViewBag.IsEnrolled = isEnrolled;

            // Truyền model khóa học sang View
            return View(course);
        }

    }
}