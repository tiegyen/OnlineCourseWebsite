using OnlineCourseWebsite.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Net;
using System.Net.Mail;

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

                // Mặc định tài khoản mới tạo phải ở trạng thái Khoá (Inactive) để bắt xác nhận email
                newAccount.Status = "Inactive";

                // Đồng bộ múi giờ Việt Nam hiện tại chuẩn chỉ luôn ghen ní
                newAccount.CreatedDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"));

                db.User_Accounts.InsertOnSubmit(newAccount);
                db.SubmitChanges();

                Student newStudent = new Student();
                newStudent.FullName = fullName;
                newStudent.Phone = phone;
                newStudent.UserID = newAccount.UserID; // Gán FK nối hai bảng

                db.Students.InsertOnSubmit(newStudent);
                db.SubmitChanges();

                // GỌI HÀM GỬI EMAIL: Chạy ngầm gửi bức thư xác nhận đến email học viên vừa nhập
                SendVerificationEmail(email, fullName, newAccount.UserID);

                // DÙNG TEMPDATA: Gửi thông điệp nhắc nhở xuyên suốt qua trang Login
                TempData["SuccessMessage"] = "Đăng ký thành công! Hệ thống đã gửi một email xác nhận đến địa chỉ: " + email + ". Vui lòng kiểm tra hộp thư (hoặc mục Spam) để kích hoạt tài khoản trước khi đăng nhập.";

                // Đăng ký xong đá văng sang trang Login đúng ý ní luôn
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

                // Lưu các thông tin ID và Quyền vào Session trước
                Session["UserId"] = account.UserID;   // Giữ ID tài khoản để dùng chung
                Session["UserRole"] = account.Role;   // Giữ Role để phân quyền chặn truy cập bậy

                // 3. PHÂN LUỒNG ĐĂNG NHẬP THEO QUYỀN (ROLE) & XỬ LÝ 🔀 ĐỔI CHUỖI HIỂN THỊ SESSION
                if (account.Role == "Admin")
                {
                    Session["UserAccount"] = "Admin"; // Gán chuỗi chữ tường minh cho Admin
                    return RedirectToAction("Dashboard", "Admin");
                }
                else if (account.Role == "Instructor")
                {
                    // Lấy thông tin chi tiết giảng viên từ bảng Instructor
                    var instructor = db.Instructors.SingleOrDefault(ins => ins.UserID == account.UserID);
                    if (instructor != null)
                    {
                        // 🔥 ĐÃ SỬA: Gán ĐÍCH DANH trường FullName dạng string chứ không gán nguyên Object nữa nhé ní!
                        Session["UserAccount"] = instructor.FullName;

                        Session["InstructorID"] = instructor.InstructorID;
                        Session["InstructorProfile"] = instructor;
                    }
                    else
                    {
                        Session["UserAccount"] = account.Email; // Dự phòng nếu chưa tạo bảng Instructor
                    }

                    // Đăng nhập đúng quyền Giảng viên thì đá thẳng vào Dashboard của Instructor liền!
                    return RedirectToAction("Dashboard", "Instructor");
                }
                else if (account.Role == "Student")
                {
                    var student = db.Students.SingleOrDefault(s => s.UserID == account.UserID);
                    if (student != null)
                    {
                        Session["UserAccount"] = student.FullName; // Gán chuỗi tên học viên để hiển thị trên Layout chung
                        Session["StudentProfile"] = student;
                        Session["StudentID"] = student.StudentID;
                    }
                    else
                    {
                        Session["UserAccount"] = account.Email;
                    }
                    return RedirectToAction("Course", "Course");
                }

                Session["UserAccount"] = account.Email;
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

        // Hàm Helper gửi mail xác nhận tài khoản tự động sử dụng SMTP Gmail
        private void SendVerificationEmail(string targetEmail, string fullName, int userId)
        {
            try
            {
                // 1. Cấu hình thông tin người gửi (Thay bằng Gmail của ní và Mật khẩu ứng dụng - App Password)
                string fromEmail = "anhthu2005.rg@gmail.com";
                string appPassword = "nzhvpcbwvwygbfhg"; // Mật khẩu ứng dụng 16 ký tự, KHÔNG PHẢI mật khẩu chính của Gmail

                // 2. Tạo link xác nhận tài khoản độc nhất dựa vào UserID
                // Khi người dùng bấm vào link này trong mail, nó sẽ kích hoạt hàm ConfirmEmail ở dưới
                string verifyLink = Url.Action("ConfirmEmail", "User", new { id = userId }, Request.Url.Scheme);

                // 3. Thiết lập nội dung thư dạng HTML nhìn cho xịn sò
                MailMessage mail = new MailMessage();
                mail.From = new MailAddress(fromEmail, "English Mastery Support");
                mail.To.Add(new MailAddress(targetEmail));
                mail.Subject = "激活账户 | Verify your English Mastery Account";

                mail.Body = $@"
            <div style='font-family: Poppins, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 12px;'>
                <h2 style='color: #764ba2; text-align: center;'>Welcome to English Mastery!</h2>
                <p>Hi <b>{fullName}</b>,</p>
                <p>Cảm ơn bạn đã đăng ký tài khoản tại hệ thống của chúng tôi. Vui lòng bấm vào nút bên dưới để xác nhận email và kích hoạt tài khoản sử dụng:</p>
                <div style='text-align: center; margin: 30px 0;'>
                    <a href='{verifyLink}' style='background: #764ba2; color: #fff; padding: 12px 25px; text-decoration: none; font-weight: 600; border-radius: 6px; display: inline-block;'>Verify Account Now</a>
                </div>
                <p style='color: #999; font-size: 13px;'>Nếu nút trên không hoạt động, bạn có thể sao chép liên kết này dán vào trình duyệt:<br/>{verifyLink}</p>
                <hr style='border: none; border-top: 1px solid #eee; margin-top: 30px;' />
                <p style='font-size: 12px; color: #aaa; text-align: center;'>Đây là email tự động, vui lòng không phản hồi thư này.</p>
            </div>";
                mail.IsBodyHtml = true;

                // 4. Cấu hình cổng kết nối SMTP Server của Google
                SmtpClient smtp = new SmtpClient("smtp.gmail.com", 587);
                smtp.EnableSsl = true;
                smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                smtp.UseDefaultCredentials = false;
                smtp.Credentials = new NetworkCredential(fromEmail, appPassword);

                // Tiến hành gửi
                smtp.Send(mail);
            }
            catch (Exception ex)
            {
                // Ghi log lỗi ra ngoài hệ thống nếu cần, tránh làm sập luồng đăng ký của khách
                System.Diagnostics.Debug.WriteLine("Lỗi gửi email: " + ex.Message);
            }
        }

        // GET: User/ConfirmEmail?id=...
        [HttpGet]
        public ActionResult ConfirmEmail(int? id)
        {
            if (id == null) return HttpNotFound();

            // Tìm tài khoản đang chờ kích hoạt
            var account = db.User_Accounts.SingleOrDefault(u => u.UserID == id.Value);
            if (account != null)
            {
                if (account.Status == "Inactive")
                {
                    account.Status = "Active"; // Kích hoạt tài khoản thành công!
                    db.SubmitChanges();

                    TempData["SuccessMessage"] = "Kích hoạt tài khoản thành công! Bạn có thể đăng nhập ngay bây giờ.";
                }
                else
                {
                    TempData["SuccessMessage"] = "Tài khoản của bạn vốn đã được kích hoạt trước đó rồi!";
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Mã kích hoạt tài khoản không hợp lệ hoặc không tồn tại!";
            }

            return RedirectToAction("Login", "User");
        }

    }
}