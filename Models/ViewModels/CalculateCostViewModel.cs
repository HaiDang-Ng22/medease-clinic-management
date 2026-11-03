using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Healthy_System.Models.ViewModels
{
    public class CalculateCostViewModel
    {
        public int AppointmentId { get; set; }
        public string AppointmentCode { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string? PatientPhone { get; set; }
        public string? PatientGender { get; set; }
        public DateTime? PatientDob { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string SpecialtyName { get; set; } = string.Empty;
        public DateTime AppointmentDate { get; set; }
        public string TimeSlot { get; set; } = string.Empty;
        public string? Diagnosis { get; set; }
        public string? Prescription { get; set; }
        public string? LabRequest { get; set; }
        public string? LabResultSummary { get; set; }

        // Bảng chi phí
        [Required(ErrorMessage = "Vui lòng nhập tiền khám bệnh")]
        [Range(0, 100000000, ErrorMessage = "Tiền khám không hợp lệ")]
        [Display(Name = "Tiền khám / Tư vấn bác sĩ (VNĐ)")]
        public decimal ConsultationFee { get; set; }

        [Range(0, 100000000, ErrorMessage = "Tiền dịch vụ không hợp lệ")]
        [Display(Name = "Tiền dịch vụ / Thủ thuật y tế (VNĐ)")]
        public decimal ServiceFee { get; set; } = 0;

        [Display(Name = "Chi tiết các dịch vụ kỹ thuật")]
        public string? ServiceDetails { get; set; }

        [Range(0, 100000000, ErrorMessage = "Tiền xét nghiệm không hợp lệ")]
        [Display(Name = "Tiền xét nghiệm / Cận lâm sàng (VNĐ)")]
        public decimal LabFee { get; set; } = 0;

        [Display(Name = "Chi tiết các hạng mục xét nghiệm")]
        public string? LabDetails { get; set; }

        [Range(0, 100000000, ErrorMessage = "Tiền thuốc không hợp lệ")]
        [Display(Name = "Tiền thuốc theo đơn (VNĐ)")]
        public decimal PrescriptionFee { get; set; } = 0;

        [Display(Name = "Chi tiết đơn thuốc kê")]
        public string? PrescriptionDetails { get; set; }

        [Display(Name = "Mức hưởng BHYT (%)")]
        public int HealthInsuranceCoveragePercent { get; set; } = 0;

        [Range(0, 100000000, ErrorMessage = "Số tiền giảm giá không hợp lệ")]
        [Display(Name = "Số tiền miễn giảm / BHYT chi trả (VNĐ)")]
        public decimal DiscountAmount { get; set; } = 0;

        [Required(ErrorMessage = "Tổng chi phí chưa được tính")]
        [Display(Name = "Tổng tiền thanh toán (VNĐ)")]
        public decimal TotalAmount { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn trạng thái thanh toán")]
        [Display(Name = "Trạng thái thanh toán")]
        public string PaymentStatus { get; set; } = "Calculated"; // Calculated, Paid

        [Display(Name = "Phương thức thanh toán")]
        public string? PaymentMethod { get; set; } = "Tiền mặt"; // Tiền mặt, Chuyển khoản / QR, Thẻ ngân hàng

        [Display(Name = "Ghi chú kế toán")]
        public string? Notes { get; set; }

        [Display(Name = "Gửi thông báo bảng chi phí đến bệnh nhân")]
        public bool NotifyPatient { get; set; } = true;

        // Dữ liệu tham khảo dịch vụ có sẵn
        public List<ServiceItemDto> AvailableServices { get; set; } = new();
    }

    public class ServiceItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Category { get; set; } = string.Empty;
    }
}
