using OnlineCourseWebsite.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace OnlineCourseWebsite.Controllers
{
    public class UserController : Controller
    {
        dbOnlineCourseDataContext db = new dbOnlineCourseDataContext();
        // GET: User
        [HttpGet]
        public ActionResult Register()
        {
            return View();
        }
        [HttpPost]
        public ActionResult Register(FormCollection collection)
        {
            var fullName = collection["FullName"];
            var phone = collection["Phone"];
            var email = collection["Email"];
            var password = collection["Password"];
            var confirmPassword = collection["ConfirmPassword"];

            bool hasError = false;

            var checkEmail = db.User_Accounts.SingleOrDefault(u => u.Email == email);

            if(checkEmail != null)
            {
                ViewBag.ErrorEmail = "This email is already registered!";
                hasError = true;
            }

            if (password != confirmPassword)
            {
                ViewBag.ErrorConfirm = "Confirm password does not match!";
                hasError = true;
            }

            if(hasError)
            {
                ViewBag.FullName = fullName;
                ViewBag.Phone = phone;
                ViewBag.Email = email;
                return View();
            }

            try
            {
                User_Account newAccount = new User_Account();
                newAccount.Email = email;
                newAccount.Password = password;

                newAccount.Role = "Student";
                newAccount.Status = "Active";
                newAccount.CreatedDate = DateTime.Now;

                db.User_Accounts.InsertOnSubmit(newAccount);
                db.SubmitChanges();

                Student newStudent = new Student();
                newStudent.FullName = fullName;
                newStudent.Phone = phone;
                newStudent.UserID = newAccount.UserID; //gán FK nối hai bảng

                db.Students.InsertOnSubmit(newStudent);
                db.SubmitChanges();

                return RedirectToAction("Login", "User");
            }
            catch (Exception ex) 
            {
                ViewBag.ErrorSystem = "Something went wrong: " + ex.Message;
                ViewBag.FullName = fullName;
                ViewBag.Phone = phone;
                ViewBag.Email = email;
                return View();
            }

        }
        [HttpGet]
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Login(FormCollection collection)
        {
            var email = collection["Email"];
            var password = collection["Password"];

            bool hasError = false;

            if(string.IsNullOrEmpty(email))
            {
                ViewBag.ErrorEmail = "Please enter your email!";
                hasError = true;
            }

            if(string.IsNullOrEmpty(password))
            {
                ViewBag.ErrorPassword = "Please enter your password!";
                hasError = true;
            }

            if(hasError)
            {
                ViewBag.Email = email;
                return View();
            }

            var account = db.User_Accounts.SingleOrDefault(u => u.Email == email && u.Password == password);

            if(account != null)
            {
                if(account.Status == "Inactive")
                {
                    ViewBag.ErrorSystem = "Your account has been locked. Please contact Admin!";
                    ViewBag.Email = email;
                    return View();
                }

                Session["UserAccount"] = account;

                if(account.Role == "Student")
                {
                    var student = db.Students.SingleOrDefault(s => s.UserID == account.UserID);
                    if(student != null)
                    {
                        Session["StudentProfile"] = student;
                    }
                }
                return RedirectToAction("Course", "Course"); 
            }
            else
            {
                ViewBag.ErrorSystem = "Invalid Email or Password!";
                ViewBag.Email = email;
                return View();
            }
        }

        public ActionResult Logout()
        {
            Session["UserAccount"] = null;
            Session["StudentProfile"] = null;
            return RedirectToAction("Course", "Course");
        }

    }
}