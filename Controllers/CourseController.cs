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
        public ActionResult Course(int? page, int? currentCategory)
        {
            int pageSize = 4;
            int pageNumber = (page ?? 1); // nếu page bị null thì mặc định lấy page 1 

            var coursesQuery = db.Courses.AsQueryable();

            if (currentCategory.HasValue && currentCategory.Value > 0)
            {
                coursesQuery = coursesQuery.Where(c => c.CategoryID == currentCategory.Value);
            }

            var allCourses = coursesQuery.OrderBy(c=>c.CourseID).ToList();  

            ViewBag.Categories = db.Categories.ToList();

            ViewBag.CurrentCategory = currentCategory;

            return View(allCourses.ToPagedList(pageNumber,pageSize));
        }
    }
}