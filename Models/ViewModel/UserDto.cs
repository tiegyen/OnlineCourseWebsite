using System.Collections.Generic;

namespace OnlineCourseWebsite.ViewModels
{
    // Dùng chung cho việc hiển thị danh sách
    public class UserDto
    {
        public int UserID { get; set; }
        public int DetailID { get; set; } // InstructorID hoặc StudentID
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Bio { get; set; } // Chỉ Instructor dùng, Student để trống
        public string Status { get; set; } // 'Active' hoặc 'Inactive'
    }

    public class UsersViewModel
    {
        public List<UserDto> Instructors { get; set; }
        public List<UserDto> Students { get; set; }
    }
}