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

        // GET: User/Register
        [HttpGet]
        public ActionResult Register()
        {
            return View();
        }

        // POST: User/Register
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

            if (checkEmail != null)
            {
                ViewBag.ErrorEmail = "This email is already registered!";
                hasError = true;
            }

            if (password != confirmPassword)
            {
                ViewBag.ErrorConfirm = "Confirm password does not match!";
                hasError = true;
            }

            if (hasError)
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
                newAccount.Role = "Student"; // Mặc định đăng ký là Student
                newAccount.Status = "Active";
                newAccount.CreatedDate = DateTime.Now;

                db.User_Accounts.InsertOnSubmit(newAccount);
                db.SubmitChanges();

                Student newStudent = new Student();
                newStudent.FullName = fullName;
                newStudent.Phone = phone;
                newStudent.UserID = newAccount.UserID; // Gán FK nối hai bảng

                db.Students.InsertOnSubmit(newStudent);
                db.SubmitChanges();

                // Đăng ký xong đá sang trang Login của User (Không lo 404)
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

        // GET: User/Login
        [HttpGet]
        public ActionResult Login()
        {
            return View();
        }

        // POST: User/Login
        [HttpPost]
        public ActionResult Login(FormCollection collection)
        {
            var email = collection["Email"];
            var password = collection["Password"];

            bool hasError = false;

            if (string.IsNullOrEmpty(email)) { ViewBag.ErrorEmail = "Please enter your email!"; hasError = true; }
            if (string.IsNullOrEmpty(password)) { ViewBag.ErrorPassword = "Please enter your password!"; hasError = true; }

            if (hasError)
            {
                ViewBag.Email = email;
                return View();
            }

            // 1. Kiểm tra tài khoản tồn tại trong hệ thống
            var account = db.User_Accounts.SingleOrDefault(u => u.Email == email && u.Password == password);

            if (account != null)
            {
                // 2. Kiểm tra trạng thái hoạt động
                if (account.Status == "Inactive")
                {
                    ViewBag.ErrorSystem = "Your account has been locked. Please contact Admin!";
                    ViewBag.Email = email;
                    return View();
                }

                // Lưu thông tin tài khoản chung vào Session
                Session["UserAccount"] = account;
                Session["UserId"] = account.UserID;   // Giữ ID tài khoản để dùng chung
                Session["UserRole"] = account.Role;   // Giữ Role để phân quyền chặn truy cập bậy

                // 3. PHÂN LUỒNG ĐĂNG NHẬP THEO QUYỀN (ROLE)
                if (account.Role == "Admin")
                {
                    return RedirectToAction("Dashboard", "Admin");
                }
                else if (account.Role == "Instructor")
                {
                    // Lấy thêm thông tin chi tiết giảng viên từ bảng Instructor (hoặc Trainer tùy DB ní đặt tên)
                    // Ở đây Gen giả định bảng của ní là Instructors nhé, ní check kỹ lại thực tế tên bảng của ní nha
                    var instructor = db.Instructors.SingleOrDefault(ins => ins.UserID == account.UserID);
                    if (instructor != null)
                    {
                        // Lưu InstructorID thực tế vào Session để mớ câu LINQ mấy trang trước lượm xài
                        Session["InstructorID"] = instructor.InstructorID;
                        Session["InstructorProfile"] = instructor;
                    }

                    // Đăng nhập đúng quyền Giảng viên thì đá thẳng vào Dashboard của Instructor liền!
                    return RedirectToAction("Dashboard", "Instructor");
                }
                else if (account.Role == "Student")
                {
                    var student = db.Students.SingleOrDefault(s => s.UserID == account.UserID);
                    if (student != null)
                    {
                        Session["StudentProfile"] = student;
                        Session["StudentID"] = student.StudentID;
                    }
                    return RedirectToAction("Course", "Course");
                }

                return RedirectToAction("Index", "Home");
            }
            else
            {
                ViewBag.ErrorSystem = "Invalid Email or Password!";
                ViewBag.Email = email;
                return View();
            }
        }

        // GET: User/Logout
        public ActionResult Logout()
        {
            // Clear sạch bóng toàn bộ các Session khi đăng xuất
            Session.Clear();
            Session.Abandon();
            return RedirectToAction("Course", "Course");
        }
    }
}