using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using OnlineCourseWebsite.Models;

namespace OnlineCourseWebsite.Controllers
{
    public class InstructorController : Controller
    {
        // Khởi tạo đối tượng kết nối Database qua LINQ to SQL
        private dbOnlineCourseDataContext db = new dbOnlineCourseDataContext();

        // GET: Instructor/Dashboard
        public ActionResult Dashboard()
        {
            // 1. Kiểm tra bảo mật xem giảng viên đã Login chưa (Đã sửa lỗi hướng về Account)
            if (Session["UserAccount"] == null || Session["UserRole"]?.ToString() != "Instructor")
            {
                return RedirectToAction("Login", "User");
            }

            // 2. Hốt đúng InstructorID thực tế từ Session để câu Query hoạt động chuẩn xác
            int currentInstructorID = Convert.ToInt32(Session["InstructorID"]);

            // 3. Tận dụng LINQ lọc ĐÚNG các khóa học của ông này để tính toán số liệu
            var courseStatsList = db.Courses
                .Where(c => c.InstructorID == currentInstructorID)
                .Select(c => new InstructorCourseStat
                {
                    CourseID = c.CourseID,
                    CourseName = c.CourseName,
                    CourseStatus = c.CourseStatus.StatusName,
                    TotalSubscribers = c.Enrollments.Count(),
                    AverageRating = c.Reviews.Any() ? (decimal)c.Reviews.Average(r => r.Rating) : 0,
                    StudyingCount = c.Enrollments.Count(e => e.LearningStatus == "Learning"),
                    CompletedCount = c.Enrollments.Count(e => e.LearningStatus == "Completed")
                }).ToList();

            // 4. Các chỉ số Counter trên Dashboard tự động ăn theo danh sách đã lọc ở trên
            var mostPopular = courseStatsList.OrderByDescending(c => c.TotalSubscribers).FirstOrDefault();

            // Lọc hành động đăng ký khóa học của RIÊNG giảng viên này (Bắn đủ data lên UI)
            var enrollActivities = db.Enrollments
                .Where(e => e.Course.InstructorID == currentInstructorID)
                .Select(e => new RecentActivityViewModel
                {
                    StudentName = e.Student.FullName,
                    CourseName = e.Course.CourseName,
                    ActivityType = "Enroll",
                    Rating = null,
                    ActionDate = e.EnrollmentDate ?? DateTime.Now
                });

            // Lọc đánh giá khóa học của RIÊNG giảng viên này (Bắn đủ data lên UI)
            var reviewActivities = db.Reviews
                .Where(r => r.Course.InstructorID == currentInstructorID)
                .Select(r => new RecentActivityViewModel
                {
                    StudentName = r.Student.FullName,
                    CourseName = r.Course.CourseName,
                    ActivityType = "Review",
                    Rating = r.Rating,
                    ActionDate = r.ReviewDate ?? DateTime.Now
                });

            var recentActivitiesList = enrollActivities.Union(reviewActivities)
                .OrderByDescending(a => a.ActionDate).Take(5).ToList();

            // 5. Đóng gói gửi qua View
            var viewModel = new InstructorDashboardViewModel
            {
                TotalCourses = courseStatsList.Count,
                TotalEnrollments = courseStatsList.Sum(c => c.TotalSubscribers),
                AverageRatingAll = courseStatsList.Any(c => c.AverageRating > 0)
                                    ? courseStatsList.Where(c => c.AverageRating > 0).Average(c => c.AverageRating)
                                    : 0,
                MostPopularCourse = mostPopular != null ? mostPopular.CourseName : "None",
                CourseStats = courseStatsList,
                RecentActivities = recentActivitiesList
            };

            return View(viewModel);
        }

        // GET: Instructor/MyCourses
        public ActionResult MyCourses()
        {
            // 1. Kiểm tra xem giảng viên đã đăng nhập chưa
            if (Session["UserAccount"] == null || Session["UserRole"]?.ToString() != "Instructor")
            {
                return RedirectToAction("Login", "User");
            }

            // 2. Lấy InstructorID thực tế từ Session của người đang đăng nhập
            int currentInstructorID = Convert.ToInt32(Session["InstructorID"]);

            var viewModel = new InstructorCoursesViewModel();

            // 3. LINQ tự động lọc dữ liệu danh sách khóa học
            viewModel.Courses = db.Courses
                .Where(c => c.InstructorID == currentInstructorID)
                .Select(c => new MyCourseItemViewModel
                {
                    CourseID = c.CourseID,
                    ImageUrl = c.Image,
                    CourseTitle = c.CourseName,
                    Duration = c.Duration,
                    CategoryName = c.Category.CategoryName,
                    Price = c.Price ?? 0,
                    StudentsEnrolled = c.Enrollments.Count(),
                    StatusName = c.CourseStatus.StatusName
                })
                .ToList();

            // 4. Load Categories và Statuses động để đưa vào Modal Thêm/Sửa
            viewModel.Categories = db.Categories
                .Select(cat => new CategoryData
                {
                    CategoryID = cat.CategoryID,
                    CategoryName = cat.CategoryName
                }).ToList();

            viewModel.Statuses = db.CourseStatus
                .Select(st => new StatusData
                {
                    StatusID = st.StatusID,
                    StatusName = st.StatusName
                }).ToList();

            return View(viewModel);
        }


    }
}