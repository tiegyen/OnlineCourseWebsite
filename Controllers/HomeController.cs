using OnlineCourseWebsite.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace OnlineCourseWebsite.Controllers
{
    public class HomeController : Controller
    {
        dbOnlineCourseDataContext db = new dbOnlineCourseDataContext();
        public ActionResult Index()
        {
            var topSubscribed = (from c in db.Courses
                              join e in db.Enrollments on c.CourseID equals e.CourseID into subscriberGroup
                              select new TargetCourseView
                              {
                                  CourseData = c,
                                  SubscriberCount = subscriberGroup.Count()
                              })
                              .OrderByDescending(x => x.SubscriberCount) //nhiều học viên thì xếp lên đầu
                              .ThenByDescending(x => x.CourseData.CourseID) //Tránh lỗi bị trùng thì dùng sort ID
                              .Select(x => x.CourseData) // trả về đúng kiểu kiểu dữ liệu bảng Course gốc
                              .Take(3) //lấy 3
                              .ToList();

            ViewBag.TopSubscribed = topSubscribed;

            var topRated = (from c in db.Courses
                            join r in db.Reviews on c.CourseID equals r.CourseID into reviewGroup
                            select new RatedCourseView
                            {
                                CourseData = c,
                                // nếu chưa có ai review thì default là 0 - tránh null
                                AverageRating = reviewGroup.Any() ? reviewGroup.Average(x => (double?)x.Rating) ?? 0 : 0,
                                ReviewCount = reviewGroup.Count()
                            })
                            .OrderByDescending(x => x.AverageRating)
                            .ThenByDescending(x => x.ReviewCount)
                            //.Select(x => x.CourseData)
                            .Take(3)
                            .ToList();
            ViewBag.TopRated = topRated;
        return View();
        }
    }

    public class TargetCourseView
    {
        public Course CourseData { get; set; }
        public int SubscriberCount { get; set; }
    }

    public class RatedCourseView
    {
        public Course CourseData { get; set; }
        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }
    }

}