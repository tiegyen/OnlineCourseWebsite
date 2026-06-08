using System;
using System.Collections.Generic;

namespace OnlineCourseWebsite.Models
{
    // Class chứa thông tin từng dòng trong bảng danh sách khóa học
    public class InstructorCourseStat
    {
        public int CourseID { get; set; }
        public string CourseName { get; set; }
        public string CourseStatus { get; set; }
        public int TotalSubscribers { get; set; }
        public decimal AverageRating { get; set; }
        public int StudyingCount { get; set; }
        public int CompletedCount { get; set; }
    }

    // Class tổng gom toàn bộ data cho Dashboard
    public class InstructorDashboardViewModel
    {
        // 4 thẻ chỉ số chính
        public int TotalCourses { get; set; }
        public int TotalEnrollments { get; set; }
        public decimal AverageRatingAll { get; set; }
        public string MostPopularCourse { get; set; }

        // Danh sách bảng thống kê chi tiết bên dưới
        public List<InstructorCourseStat> CourseStats { get; set; }

        // THÊM THUỘC TÍNH 
        public List<RecentActivityViewModel> RecentActivities { get; set; }
    }

    public class RecentActivityViewModel
    {
        public string StudentName { get; set; }
        public string CourseName { get; set; }
        public string ActivityType { get; set; } // "Enroll" hoặc "Review"
        public int? Rating { get; set; }          // Chỉ dùng nếu là Review
        public DateTime ActionDate { get; set; }
    }

    public class MyCourseItemViewModel
    {
        public int CourseID { get; set; }
        public string ImageUrl { get; set; }
        public string CourseTitle { get; set; }
        public string Duration { get; set; }
        public string CategoryName { get; set; }
        public decimal Price { get; set; }
        public int StudentsEnrolled { get; set; }
        public string StatusName { get; set; }
    }

    public class InstructorCoursesViewModel
    {
        public List<MyCourseItemViewModel> Courses { get; set; }

        // Data động để load lên các thẻ <select> trong Modal Tạo/Sửa
        public List<CategoryData> Categories { get; set; }
        public List<StatusData> Statuses { get; set; }
    }

    public class CategoryData
    {
        public int CategoryID { get; set; }
        public string CategoryName { get; set; }
    }

    public class StatusData
    {
        public int StatusID { get; set; }
        public string StatusName { get; set; }
    }


}