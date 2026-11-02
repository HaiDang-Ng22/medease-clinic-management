using System.ComponentModel.DataAnnotations;

namespace Healthy_System.Models.ViewModels
{
    public class PaymentViewModel
    {
        public int AppointmentId { get; set; }
        public string AppointmentCode { get; set; }
        public string InvoiceCode { get; set; }
        public string PatientName { get; set; }
        public string PatientPhone { get; set; }
        public string DoctorName { get; set; }
        public string SpecialtyName { get; set; }
        public decimal ConsultationFee { get; set; }
        public decimal TotalAmount { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn phương thức thanh toán.")]
        public string PaymentMethod { get; set; }

        public string CashierName { get; set; }
    }
}