using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Healthy_System.Models
{
    public class PatientCost
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int AppointmentId { get; set; }

        [ForeignKey("AppointmentId")]
        public Appointment? Appointment { get; set; }

        public int? AccountantId { get; set; }

        [ForeignKey("AccountantId")]
        public User? Accountant { get; set; }

        // Tiền công khám bác sĩ
        [Column(TypeName = "decimal(18,2)")]
        public decimal ConsultationFee { get; set; } = 0;

        // Tiền dịch vụ kỹ thuật / thủ thuật
        [Column(TypeName = "decimal(18,2)")]
        public decimal ServiceFee { get; set; } = 0;

        [StringLength(500)]
        public string? ServiceDetails { get; set; }

        // Tiền xét nghiệm / Cận lâm sàng
        [Column(TypeName = "decimal(18,2)")]
        public decimal LabFee { get; set; } = 0;

        [StringLength(500)]
        public string? LabDetails { get; set; }

        // Tiền thuốc theo đơn
        [Column(TypeName = "decimal(18,2)")]
        public decimal PrescriptionFee { get; set; } = 0;

        [StringLength(1000)]
        public string? PrescriptionDetails { get; set; }

        // Tỷ lệ % BHYT chi trả (0, 20, 80, 100)
        public int HealthInsuranceCoveragePercent { get; set; } = 0;

        // Số tiền giảm trừ / BHYT chi trả
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; } = 0;

        // Tổng chi phí bệnh nhân phải thanh toán
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; } = 0;

        // Trạng thái thanh toán: Pending (Chờ tính phí), Calculated (Đã tính chi phí), Paid (Đã thanh toán)
        [Required]
        [StringLength(50)]
        public string PaymentStatus { get; set; } = "Calculated";

        [StringLength(50)]
        public string? PaymentMethod { get; set; } // Tiền mặt, Chuyển khoản, Thẻ ATM/Visa

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? PaidAt { get; set; }
    }
}
