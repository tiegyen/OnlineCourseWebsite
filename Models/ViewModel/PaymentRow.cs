using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OnlineCourseWebsite.Models.ViewModel
{
    public class PaymentRow
    {
        public int PaymentID { get; set; }
        public string CourseName { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; }
        public string PaymentStatus { get; set; }
        public DateTime? PaymentDate { get; set; } //add payment date
    }
}