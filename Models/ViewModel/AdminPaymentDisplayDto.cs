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

        // 🔥 THÊM 2 THUỘC TÍNH NÀY:
        public DateTime? UpdatedDate { get; set; } // Lấy ngày PaymentDate để tính thời gian sau khi bấm nút
        public string StatusNotes { get; set; }    // Tận dụng cột mô tả để lưu trạng thái gốc (nếu DB có hoặc ta giả định)
    }
}