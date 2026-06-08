using OnlineCourseWebsite.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

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

        // POST: Instructor/SaveCourse
        [HttpPost]
        [ValidateInput(false)] // Cho phép nhập ký tự đặc biệt ở Description nếu cần
        public ActionResult SaveCourse(FormCollection collection, HttpPostedFileBase ImageFile)
        {
            // 1. Chặn bảo mật trước, phòng hờ mất Session
            if (Session["UserAccount"] == null || Session["UserRole"]?.ToString() != "Instructor")
            {
                return RedirectToAction("Login", "User");
            }

            int currentInstructorID = Convert.ToInt32(Session["InstructorID"]);

            // 2. Hốt dữ liệu từ Form Modal gửi lên
            int courseID = Convert.ToInt32(collection["CourseID"]);
            string courseName = collection["CourseName"];
            int categoryID = Convert.ToInt32(collection["CategoryID"]);
            int statusID = Convert.ToInt32(collection["StatusID"]);
            decimal price = Convert.ToDecimal(collection["Price"]);
            string duration = collection["Duration"];
            string description = collection["Description"];

            try
            {
                // Xử lý upload hình ảnh (nếu giảng viên có chọn file mới)
                string imageUrl = null;
                if (ImageFile != null && ImageFile.ContentLength > 0)
                {
                    string filename = System.IO.Path.GetFileName(ImageFile.FileName);
                    string path = System.IO.Path.Combine(Server.MapPath("~/Content/images/"), filename);
                    ImageFile.SaveAs(path);
                    imageUrl = "~/Content/images/" + filename; // Lưu đường dẫn tương đối vào DB
                }

                if (courseID == 0)
                {
                    // THỂ LOẠI 1: TẠO MỚI KHÓA HỌC (Id gửi lên bằng 0)
                    // Lưu ý: Ní check lại chuẩn xác tên bảng và tên cột trong file .dbml của ní nha
                    Course newCourse = new Course();
                    newCourse.CourseName = courseName;
                    newCourse.CategoryID = categoryID;
                    newCourse.StatusID = statusID; // Hoặc CourseStatusID tùy thuộc tên cột FK của ní
                    newCourse.Price = price;
                    newCourse.Duration = duration;
                    newCourse.Description = description;
                    newCourse.InstructorID = currentInstructorID; // Gán ID giảng viên đang login
                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        newCourse.Image = imageUrl;
                    }

                    db.Courses.InsertOnSubmit(newCourse);
                }
                else
                {
                    // THỂ LOẠI 2: CẬP NHẬT KHÓA HỌC ĐÃ CÓ (Id gửi lên khác 0)
                    var editCourse = db.Courses.SingleOrDefault(c => c.CourseID == courseID && c.InstructorID == currentInstructorID);
                    if (editCourse != null)
                    {
                        editCourse.CourseName = courseName;
                        editCourse.CategoryID = categoryID;
                        editCourse.StatusID = statusID;
                        editCourse.Price = price;
                        editCourse.Duration = duration;
                        editCourse.Description = description;
                        if (!string.IsNullOrEmpty(imageUrl))
                        {
                            editCourse.Image = imageUrl;
                        }
                    }
                }

                // Lưu thay đổi xuống Database
                db.SubmitChanges();

                // Lưu thành công thì load lại đúng trang danh sách khóa học của họ
                return RedirectToAction("MyCourses");
            }
            catch (Exception ex)
            {
                // Nếu dính lỗi gì đó thì tạm thời ném qua ViewBag hoặc quay về trang cũ để check
                TempData["Error"] = "Lỗi lưu dữ liệu: " + ex.Message;
                return RedirectToAction("MyCourses");
            }
        }

        [HttpGet]

        public ActionResult Curriculum(int? id) // 🌟 Thêm dấu ? vào đây ní ơi
        {
            // Nếu không có id truyền vào, quay xe về trang danh sách khóa học liền
            if (id == null)
            {
                return RedirectToAction("MyCourses");
            }

            // Dùng id.Value vì lúc này id đã là Nullable
            var course = db.Courses.SingleOrDefault(c => c.CourseID == id.Value);
            if (course == null) return HttpNotFound();

            var lessons = db.Lessons
                            .Where(l => l.CourseID == id.Value)
                            .OrderBy(l => l.LessonOrder)
                            .ToList();

            ViewBag.Lessons = lessons;
            return View(course);
        }

        [HttpPost]
        public ActionResult SaveLesson(int CourseID, int LessonID, string SecName, string LessonTitle, string VideoURL, HttpPostedFileBase UploadedMaterial)
        {
            // 1. Tìm xem bài học đã tồn tại chưa bằng LINQ to SQL
            var lesson = db.Lessons.FirstOrDefault(l => l.LessonID == LessonID);

            if (lesson == null) // Nếu KHÔNG tìm thấy -> Tạo bài học MỚI
            {
                // Tính toán LessonOrder tự động tăng dựa trên CourseID
                int maxOrder = db.Lessons
                                 .Where(l => l.CourseID == CourseID)
                                 .Select(l => (int?)l.LessonOrder)
                                 .FirstOrDefault() ?? 0;

                lesson = new Lesson
                {
                    CourseID = CourseID,
                    SectionName = SecName, // 🌟 Gán biến SecName hứng từ View vào thuộc tính SectionName của DB
                    LessonTitle = LessonTitle,
                    VideoURL = VideoURL,
                    LessonOrder = maxOrder + 1,
                    LessonContent = "" // Cho chuỗi rỗng tránh null nếu DB không cho phép null
                };

                // Cú pháp chuẩn LINQ to SQL
                db.Lessons.InsertOnSubmit(lesson);
            }
            else // Nếu TÌM THẤY -> Cập nhật data bài cũ
            {
                lesson.SectionName = SecName; // 🌟 Cập nhật lại SectionName bằng biến SecName từ View gửi lên
                lesson.LessonTitle = LessonTitle;
                lesson.VideoURL = VideoURL;
            }

            // Đẩy lệnh xuống SQL Server để lưu và phát sinh ra LessonID tự động tăng
            db.SubmitChanges();

            // 2. Xử lý Upload file đính kèm (Nối vào bảng CourseMaterial)
            if (UploadedMaterial != null && UploadedMaterial.ContentLength > 0)
            {
                string fileName = Path.GetFileName(UploadedMaterial.FileName);
                string folderPath = Server.MapPath("~/Content/materials/");

                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string physicalPath = Path.Combine(folderPath, fileName);
                UploadedMaterial.SaveAs(physicalPath);

                // Tạo Object Material mới đắp vào DB theo đúng cấu trúc bảng của ní
                var material = new CourseMaterial
                {
                    FileName = fileName,
                    FileURL = "/Content/materials/" + fileName,
                    UploadDate = DateTime.Now,
                    LessonID = lesson.LessonID // Ăn theo ID của bài học vừa lưu hoặc vừa tìm thấy ở trên
                };

                db.CourseMaterials.InsertOnSubmit(material);
                db.SubmitChanges();
            }

            // Quay xe định tuyến về trang quản lý chương trình học của khóa học này
            return RedirectToAction("Curriculum", new { id = CourseID });
        }

        [HttpGet]
        public ActionResult DeleteLesson(int id, int courseId)
        {
            // Bốc chính xác bài học cần xóa
            var lesson = db.Lessons.FirstOrDefault(l => l.LessonID == id);

            if (lesson != null)
            {
                // 🌟 CHỖ NÀY NÍ ƠI: Dùng DeleteOnSubmit thay vì Remove
                db.Lessons.DeleteOnSubmit(lesson);
                db.SubmitChanges();
            }

            return RedirectToAction("Curriculum", new { id = courseId });
        }

        // GET: Instructor/EnrolledStudents
        public ActionResult EnrolledStudents()
        {
            // 1. Kiểm tra bảo mật
            if (Session["UserAccount"] == null || Session["UserRole"]?.ToString() != "Instructor")
            {
                return RedirectToAction("Login", "User");
            }

            int currentInstructorID = Convert.ToInt32(Session["InstructorID"]);

            // 2. Truy vấn dữ liệu: Chỉ lấy học viên đăng ký khóa học của giảng viên này
            var students = db.Enrollments
                .Where(e => e.Course.InstructorID == currentInstructorID)
                .Select(e => new EnrolledStudentViewModel
                {
                    StudentName = e.Student.FullName,
                    Email = e.Student.User_Account.Email, // Lưu ý: kiểm tra xem bảng User_Account có phải là User_Account không nhé
                    CourseName = e.Course.CourseName,
                    EnrollmentDate = e.EnrollmentDate ?? DateTime.Now,
                    PaymentStatus = e.Payments.FirstOrDefault() != null ? e.Payments.FirstOrDefault().PaymentStatus : "Pending",
                    Progress = e.Progress ?? 0
                })
                .OrderByDescending(e => e.EnrollmentDate)
                .ToList();

            return View(students);
        }

    }
}