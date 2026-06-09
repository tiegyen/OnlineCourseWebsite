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
            // 1. Kiểm tra bảo mật xem giảng viên đã Login chưa
            if (Session["UserAccount"] == null || Session["UserRole"]?.ToString() != "Instructor")
            {
                return RedirectToAction("Login", "User");
            }

            int currentInstructorID = Convert.ToInt32(Session["InstructorID"]);

            // 2. Tận dụng LINQ lọc ĐÚNG các khóa học của giảng viên để tính toán số liệu từng khóa
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

            var mostPopular = courseStatsList.OrderByDescending(c => c.TotalSubscribers).FirstOrDefault();

            // 3. Lọc hoạt động đăng ký mới
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

            // 4. Lọc hoạt động đánh giá mới
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

            // 🔥 TỐI ƯU: Chỉ truy vấn bảng Reviews đúng 1 lần duy nhất để lấy danh sách điểm số, giảm tải cho SQL Server
            var instructorRatings = db.Reviews
                                      .Where(r => r.Course.InstructorID == currentInstructorID)
                                      .Select(r => r.Rating)
                                      .ToList();

            // 5. Đóng gói gửi qua View
            var viewModel = new InstructorDashboardViewModel
            {
                TotalCourses = courseStatsList.Count,
                TotalEnrollments = courseStatsList.Sum(c => c.TotalSubscribers),

                // Tính toán trực tiếp trên List RAM vừa hốt về, không ép SQL chạy lại
                AverageRatingAll = instructorRatings.Any() ? (decimal)instructorRatings.Average() : 0,

                MostPopularCourse = mostPopular != null ? mostPopular.CourseName : "None",
                CourseStats = courseStatsList,
                RecentActivities = recentActivitiesList
            };

            return View(viewModel);
        }

        // GET: Instructor/MyCourses
        public ActionResult MyCourses()
        {
            if (Session["UserAccount"] == null || Session["UserRole"]?.ToString() != "Instructor")
            {
                return RedirectToAction("Login", "User");
            }

            int currentInstructorID = Convert.ToInt32(Session["InstructorID"]);
            var viewModel = new InstructorCoursesViewModel();

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
        [ValidateInput(false)]
        public ActionResult SaveCourse(FormCollection collection, HttpPostedFileBase ImageFile)
        {
            if (Session["UserAccount"] == null || Session["UserRole"]?.ToString() != "Instructor")
            {
                return RedirectToAction("Login", "User");
            }

            int currentInstructorID = Convert.ToInt32(Session["InstructorID"]);

            // Hốt dữ liệu an toàn
            int courseID = Convert.ToInt32(collection["CourseID"]);
            string courseName = collection["CourseName"];
            int categoryID = Convert.ToInt32(collection["CategoryID"]);
            int statusID = Convert.ToInt32(collection["StatusID"]);
            string duration = collection["Duration"];
            string description = collection["Description"];

            // 🔥 TỐI ƯU: Ép kiểu an toàn bằng TryParse tránh sập web khi nhập sai định dạng số tiền
            decimal price = 0;
            decimal.TryParse(collection["Price"], out price);

            try
            {
                string imageUrl = null;
                if (ImageFile != null && ImageFile.ContentLength > 0)
                {
                    string filename = Path.GetFileName(ImageFile.FileName);
                    string path = Path.Combine(Server.MapPath("~/Content/images/"), filename);
                    ImageFile.SaveAs(path);
                    imageUrl = "~/Content/images/" + filename;
                }

                if (courseID == 0)
                {
                    Course newCourse = new Course
                    {
                        CourseName = courseName,
                        CategoryID = categoryID,
                        StatusID = statusID,
                        Price = price,
                        Duration = duration,
                        Description = description,
                        InstructorID = currentInstructorID
                    };
                    if (!string.IsNullOrEmpty(imageUrl)) newCourse.Image = imageUrl;

                    db.Courses.InsertOnSubmit(newCourse);
                }
                else
                {
                    var editCourse = db.Courses.SingleOrDefault(c => c.CourseID == courseID && c.InstructorID == currentInstructorID);
                    if (editCourse != null)
                    {
                        editCourse.CourseName = courseName;
                        editCourse.CategoryID = categoryID;
                        editCourse.StatusID = statusID;
                        editCourse.Price = price;
                        editCourse.Duration = duration;
                        editCourse.Description = description;
                        if (!string.IsNullOrEmpty(imageUrl)) editCourse.Image = imageUrl;
                    }
                }

                db.SubmitChanges();
                return RedirectToAction("MyCourses");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi lưu dữ liệu: " + ex.Message;
                return RedirectToAction("MyCourses");
            }
        }

        // GET: Instructor/Curriculum/id
        [HttpGet]
        public ActionResult Curriculum(int? id)
        {
            if (id == null) return RedirectToAction("MyCourses");

            var course = db.Courses.SingleOrDefault(c => c.CourseID == id.Value);
            if (course == null) return HttpNotFound();

            var lessons = db.Lessons
                            .Where(l => l.CourseID == id.Value)
                            .OrderBy(l => l.LessonOrder)
                            .ToList();

            ViewBag.Lessons = lessons;
            return View(course);
        }

        // POST: Instructor/SaveLesson
        [HttpPost]
        public ActionResult SaveLesson(int CourseID, int LessonID, string SecName, string LessonTitle, string VideoURL, HttpPostedFileBase UploadedMaterial)
        {
            var lesson = db.Lessons.FirstOrDefault(l => l.LessonID == LessonID);

            if (lesson == null)
            {
                int maxOrder = db.Lessons
                                 .Where(l => l.CourseID == CourseID)
                                 .Select(l => (int?)l.LessonOrder)
                                 .FirstOrDefault() ?? 0;

                lesson = new Lesson
                {
                    CourseID = CourseID,
                    SectionName = SecName,
                    LessonTitle = LessonTitle,
                    VideoURL = VideoURL,
                    LessonOrder = maxOrder + 1,
                    LessonContent = ""
                };

                db.Lessons.InsertOnSubmit(lesson);
            }
            else
            {
                lesson.SectionName = SecName;
                lesson.LessonTitle = LessonTitle;
                lesson.VideoURL = VideoURL;
            }

            db.SubmitChanges();

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

                var material = new CourseMaterial
                {
                    FileName = fileName,
                    FileURL = "/Content/materials/" + fileName,
                    UploadDate = DateTime.Now,
                    LessonID = lesson.LessonID
                };

                db.CourseMaterials.InsertOnSubmit(material);
                db.SubmitChanges();
            }

            return RedirectToAction("Curriculum", new { id = CourseID });
        }

        // GET: Instructor/DeleteLesson
        [HttpGet]
        public ActionResult DeleteLesson(int id, int courseId)
        {
            var lesson = db.Lessons.FirstOrDefault(l => l.LessonID == id);

            if (lesson != null)
            {
                // 🔥 TỐI ƯU: Tìm và xóa các file vật lý của bài học này trên ổ cứng Server trước khi xóa DB
                var materials = db.CourseMaterials.Where(m => m.LessonID == id).ToList();
                foreach (var mat in materials)
                {
                    string physicalPath = Server.MapPath("~" + mat.FileURL);
                    if (System.IO.File.Exists(physicalPath))
                    {
                        System.IO.File.Delete(physicalPath); // Xóa file thực tế
                    }
                }

                // Do cơ chế Cascade Delete hoặc phải xóa Material trước, ta xóa Submit luôn
                db.CourseMaterials.DeleteAllOnSubmit(materials);
                db.Lessons.DeleteOnSubmit(lesson);
                db.SubmitChanges();
            }

            return RedirectToAction("Curriculum", new { id = courseId });
        }
        // GET: Instructor/EnrolledStudents
        public ActionResult EnrolledStudents(int? categoryId)
        {
            // 1. Kiểm tra bảo mật
            if (Session["UserAccount"] == null || Session["UserRole"]?.ToString() != "Instructor")
            {
                return RedirectToAction("Login", "User");
            }

            int currentInstructorID = Convert.ToInt32(Session["InstructorID"]);

            // 2. Load danh sách các Category động lên thanh Filter (Chỉ lấy các category của những khóa học ông này dạy)
            var categories = db.Courses
    .Where(c => c.InstructorID == currentInstructorID)
    .Select(c => c.Category) // Lấy thực thể Category từ DB
    .Distinct()
    .ToList() // Ép về List trên RAM trước
    .Select(cat => new SelectListItem
    {
        Value = cat.CategoryID.ToString(),
        Text = cat.CategoryName,
        Selected = (categoryId.HasValue && cat.CategoryID == categoryId.Value)
    })
    .ToList();

            ViewBag.Categories = categories;
            ViewBag.SelectedCategory = categoryId; // Giữ lại ID đã chọn để UI hiển thị chính xác

            // 3. Truy vấn dữ liệu học viên (Khởi tạo Query)
            var enrollmentQuery = db.Enrollments.Where(e => e.Course.InstructorID == currentInstructorID);

            // 🔥 LỌC THEO CATEGORY: Nếu ní chọn một danh mục cụ thể, LINQ sẽ đắp thêm điều kiện lọc
            if (categoryId.HasValue && categoryId.Value > 0)
            {
                enrollmentQuery = enrollmentQuery.Where(e => e.Course.CategoryID == categoryId.Value);
            }

            // 4. Đổ dữ liệu về ViewModel
            var students = enrollmentQuery
                .Select(e => new EnrolledStudentViewModel
                {
                    StudentName = e.Student.FullName,
                    Email = e.Student.User_Account.Email,
                    CourseName = e.Course.CourseName,
                    EnrollmentDate = e.EnrollmentDate ?? DateTime.Now,
                    PaymentStatus = e.Payments.FirstOrDefault() != null ? e.Payments.FirstOrDefault().PaymentStatus : "Pending",
                    Progress = e.Progress ?? 0
                })
                .OrderByDescending(e => e.EnrollmentDate)
                .ToList();

            return View(students);
        }

        // GET: Instructor/Reviews
        [HttpGet]
        public ActionResult Reviews()
        {
            if (Session["UserAccount"] == null || Session["UserRole"]?.ToString() != "Instructor")
            {
                return RedirectToAction("Login", "User");
            }

            int currentInstructorID = Convert.ToInt32(Session["InstructorID"]);

            var reviewQuery = db.Reviews
                                .Where(r => r.Course.InstructorID == currentInstructorID)
                                .Select(r => new InstructorReviewItemViewModel
                                {
                                    ReviewID = r.ReviewID,
                                    Rating = r.Rating,
                                    Comment = r.Comment,
                                    ReviewDate = r.ReviewDate ?? DateTime.Now,
                                    StudentName = r.Student.FullName,
                                    CourseID = r.CourseID,
                                    CourseName = r.Course.CourseName
                                })
                                .OrderByDescending(r => r.ReviewDate)
                                .ToList();

            int totalReviews = reviewQuery.Count;

            // Ép kiểu trực tiếp gọn gàng từ dữ liệu danh sách đã tải
            decimal averageRating = totalReviews > 0
                ? Math.Round((decimal)reviewQuery.Average(r => r.Rating), 1)
                : 0.0m;

            var viewModel = new InstructorReviewsViewModel
            {
                AverageRating = averageRating,
                TotalReviews = totalReviews,
                Reviews = reviewQuery
            };

            return View(viewModel);
        }

        // GET: Instructor/DeleteCourse/id
        [HttpGet]
        [Route("Instructor/DeleteCourse/{id}")]
        public ActionResult DeleteCourse(int id)
        {
            // 1. Kiểm tra quyền bảo mật
            if (Session["UserAccount"] == null || Session["UserRole"]?.ToString() != "Instructor")
            {
                return RedirectToAction("Login", "User");
            }

            int currentInstructorID = Convert.ToInt32(Session["InstructorID"]);

            // 2. Tìm khóa học thuộc về đúng giảng viên này
            var course = db.Courses.SingleOrDefault(c => c.CourseID == id && c.InstructorID == currentInstructorID);

            if (course != null)
            {
                try
                {
                    // a. Xóa bài học và học liệu trước nếu có
                    var lessons = db.Lessons.Where(l => l.CourseID == id).ToList();
                    foreach (var l in lessons)
                    {
                        var materials = db.CourseMaterials.Where(m => m.LessonID == l.LessonID).ToList();

                        // Xóa file vật lý trên Server trước khi xóa bản ghi trong DB
                        foreach (var mat in materials)
                        {
                            string physicalPath = Server.MapPath("~" + mat.FileURL);
                            if (System.IO.File.Exists(physicalPath))
                            {
                                System.IO.File.Delete(physicalPath);
                            }
                        }
                        db.CourseMaterials.DeleteAllOnSubmit(materials);
                    }
                    db.Lessons.DeleteAllOnSubmit(lessons);

                    // b. Xóa các Reviews (Đánh giá) liên quan đến khóa học này
                    var reviews = db.Reviews.Where(r => r.CourseID == id).ToList();
                    db.Reviews.DeleteAllOnSubmit(reviews);

                    // c. Xóa các Enrollments (Học viên đăng ký) liên quan đến khóa học này
                    // LƯU Ý: Nếu có bảng Payments liên kết với Enrollment, ní cần xóa Payment trước luôn nhé!
                    var enrollments = db.Enrollments.Where(e => e.CourseID == id).ToList();
                    foreach (var e in enrollments)
                    {
                        var payments = db.Payments.Where(p => p.EnrollmentID == e.EnrollmentID).ToList();
                        db.Payments.DeleteAllOnSubmit(payments);
                    }
                    db.Enrollments.DeleteAllOnSubmit(enrollments);

                    // d. Cuối cùng xóa khóa học
                    db.Courses.DeleteOnSubmit(course);
                    db.SubmitChanges();

                    TempData["Success"] = "Course deleted successfully!";
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Cannot delete course: " + ex.Message;
                }
            }

            return RedirectToAction("MyCourses");
        }

        // Action xuất file danh sách học viên đăng ký theo Instructor (Hỗ trợ giữ nguyên bộ lọc Category)
        public ActionResult ExportEnrolledStudents(int? categoryId)
        {
            // 1. Kiểm tra bảo mật quyền Instructor
            if (Session["UserAccount"] == null || Session["UserRole"]?.ToString() != "Instructor")
            {
                return RedirectToAction("Login", "User");
            }

            int currentInstructorID = Convert.ToInt32(Session["InstructorID"]);

            // 2. Tạo Query cơ sở: Chỉ lấy học viên đăng ký các khóa của giảng viên này
            var enrollmentQuery = db.Enrollments.Where(e => e.Course.InstructorID == currentInstructorID);

            // Nếu có bộ lọc categoryId thì áp dụng lọc tiếp
            if (categoryId.HasValue && categoryId.Value > 0)
            {
                enrollmentQuery = enrollmentQuery.Where(e => e.Course.CategoryID == categoryId.Value);
            }

            // 3. Lấy dữ liệu về RAM để chuẩn bị ghi file
            var studentList = enrollmentQuery
                .Select(e => new EnrolledStudentViewModel
                {
                    StudentName = e.Student.FullName,
                    Email = e.Student.User_Account.Email,
                    CourseName = e.Course.CourseName,
                    EnrollmentDate = e.EnrollmentDate ?? DateTime.Now,
                    PaymentStatus = e.Payments.FirstOrDefault() != null ? e.Payments.FirstOrDefault().PaymentStatus : "Pending",
                    Progress = e.Progress ?? 0
                })
                .OrderByDescending(e => e.EnrollmentDate)
                .ToList();

            // 4. Khởi tạo StringWriter viết nội dung CSV
            var sw = new StringWriter();

            // Viết tiêu đề cột (Bằng tiếng Anh hoặc tiếng Việt không dấu/có dấu đều được nhờ UTF-8 BOM)
            sw.WriteLine("Student Name,Email,Course Name,Enrollment Date,Payment Status,Progress (%)");

            // Duyệt qua danh sách ghi từng dòng dữ liệu
            foreach (var std in studentList)
            {
                // Xử lý chuỗi tên khóa học hoặc tên học viên nếu lỡ có dấu phẩy tránh làm lệch cột CSV
                string cleanCourseName = std.CourseName.Replace(",", " ");
                string cleanStudentName = std.StudentName.Replace(",", " ");
                string formattedDate = std.EnrollmentDate.ToString("dd/MM/yyyy HH:mm:ss");

                sw.WriteLine($"\"{cleanStudentName}\",\"{std.Email}\",\"{cleanCourseName}\",\"{formattedDate}\",\"{std.PaymentStatus}\",{std.Progress}");
            }

            // 5. Đóng gói Byte Order Mark (BOM) để Excel nhận diện chữ có dấu mượt mà
            var stringData = sw.ToString();
            var bytes = System.Text.Encoding.UTF8.GetBytes(stringData);
            var result = new byte[bytes.Length + 3];
            result[0] = 0xEF; // BOM UTF-8
            result[1] = 0xBB;
            result[2] = 0xBF;
            Array.Copy(bytes, 0, result, 3, bytes.Length);

            // 6. Trả file download về máy
            string fileName = "Enrolled_Students_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
            return File(result, "text/csv", fileName);
        }

        // GET: Instructor/Profile
        [HttpGet]
        public ActionResult Profile()
        {
            if (Session["UserAccount"] == null || Session["UserRole"]?.ToString() != "Instructor")
            {
                return RedirectToAction("Login", "User");
            }

            int currentInstructorID = Convert.ToInt32(Session["InstructorID"]);

            // Tìm Instructor và dùng LINQ để ép load luôn cả bảng User_Account đi kèm (để lấy Email)
            var instructor = db.Instructors.SingleOrDefault(i => i.InstructorID == currentInstructorID);
            if (instructor == null)
            {
                return HttpNotFound();
            }

            return View(instructor);
        }

        // POST: Instructor/UpdateProfile
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult UpdateProfile(FormCollection collection, HttpPostedFileBase AvatarFile)
        {
            if (Session["UserAccount"] == null || Session["UserRole"]?.ToString() != "Instructor")
            {
                return RedirectToAction("Login", "User");
            }

            int currentInstructorID = Convert.ToInt32(Session["InstructorID"]);
            var instructor = db.Instructors.SingleOrDefault(i => i.InstructorID == currentInstructorID);

            if (instructor == null)
            {
                return HttpNotFound();
            }

            try
            {
                // 1. Cập nhật các thông tin theo ĐÚNG tên cột DB của ní
                instructor.FullName = collection["FullName"];
                instructor.Phone = collection["Phone"];
                instructor.Address = collection["Address"];
                instructor.Bio = collection["Bio"];

                // 2. Xử lý up ảnh và lưu vào cột [Image] của bảng Instructor
                if (AvatarFile != null && AvatarFile.ContentLength > 0)
                {
                    string filename = Path.GetFileName(AvatarFile.FileName);
                    string path = Path.Combine(Server.MapPath("~/Content/images/"), filename);
                    AvatarFile.SaveAs(path);

                    // Map đúng vào cột Image của ní nè
                    instructor.Image = "~/Content/images/" + filename;
                }

                // 3. Đẩy thay đổi xuống SQL Server
                db.SubmitChanges();

                // Đồng bộ lại Session hiển thị tên mới trên Layout ngay lập tức
                Session["UserAccount"] = instructor.FullName;

                TempData["Success"] = "Profile updated successfully!";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi cập nhật: " + ex.Message;
                return RedirectToAction("Profile");
            }
        }


    }
}