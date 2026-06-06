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
            var currentCourses = from c in db.Courses select c;

            return View(currentCourses.ToList());
        }
    }
}