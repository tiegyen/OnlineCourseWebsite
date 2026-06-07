using System;

namespace OnlineCourseWebsite.ViewModels
{
    public class AdminPaymentDisplayDto
    {
        public int PaymentID { get; set; }
        public int EnrollmentID { get; set; }
        public string StudentName { get; set; }
        public string CourseName { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; }
        public string PaymentStatus { get; set; }
        public DateTime PaymentDate { get; set; }
        public double Progress { get; set; } // Thêm trường này để check tiến độ
    }
}