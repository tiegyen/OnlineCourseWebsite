using System.Collections.Generic;
using OnlineCourseWebsite.Models; 


   

    namespace OnlineCourseWebsite.ViewModels
    {
    // Class phụ để chứa dữ liệu sau khi JOIN LINQ
    public class CourseDisplayDto
    {
        public int CourseID { get; set; }
        public string CourseName { get; set; }
        public decimal Price { get; set; }
        public string CategoryName { get; set; }
        public string InstructorName { get; set; }
        public int InstructorID { get; set; }
        public string StatusName { get; set; }
        public int StatusID { get; set; }

        // ĐẮP THÊM 2 DÒNG NÀY VÀO ĐỂ FIX LỖI 
        public string Image { get; set; }
        public string Description { get; set; }

        public string Duration { get; set; }
        public int CategoryID { get; set; }
    }



    public class CoursesViewModel
    {
        public List<CourseDisplayDto> Courses { get; set; }
        public List<Category> Categories { get; set; }  // Class Category từ LINQ to SQL
        public List<Instructor> Instructors { get; set; } // Class Instructor từ LINQ to SQL
        public List<CourseStatus> CourseStatuses { get; set; }
    }
}
