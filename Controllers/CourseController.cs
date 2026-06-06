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

        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return RedirectToAction("Course");
            }

            var course = db.Courses.SingleOrDefault(c => c.CourseID == id.Value);

            if(course == null)
            {
                return HttpNotFound();
            }

            var reviews = db.Reviews.Where(r => r.CourseID == id.Value).OrderByDescending(r => r.ReviewDate).ToList();
            ViewBag.Reviews = reviews;

            return View(course);
        }

    }
}