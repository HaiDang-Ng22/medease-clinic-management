using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Healthy_System.Models
{
    // US29: Nhập kết quả xét nghiệm / siêu âm / X-quang
    // US30: Đính kèm file hình ảnh (X-quang, siêu âm)
    public class LabResult
    {
        [Key]
        public int Id { get; set; }

        // Liên kết với lịch hẹn (phiếu yêu cầu XN đã có)
        [Required]
        public int AppointmentId { get; set; }

        [ForeignKey("AppointmentId")]
        public Appointment? Appointment { get; set; }

        // Kỹ thuật viên nhập kết quả
        [Required]
        public int LabTechId { get; set; }

        [ForeignKey("LabTechId")]
        public User? LabTech { get; set; }

        // US29: Kết quả xét nghiệm
        [Required(ErrorMessage = "Vui lòng nhập kết quả xét nghiệm")]
        [StringLength(2000)]
        public string ResultSummary { get; set; } = string.Empty; // Tóm tắt kết quả

        [StringLength(5000)]
        public string? DetailedResult { get; set; } // Kết quả chi tiết từng chỉ số

        [StringLength(1000)]
        public string? LabTechNotes { get; set; } // Ghi chú của KTV

        // US30: Đính kèm file hình ảnh
        [StringLength(500)]
        public string? ImageFilePath { get; set; } // Đường dẫn file X-quang / siêu âm

        [StringLength(255)]
        public string? ImageFileName { get; set; } // Tên file gốc

        [StringLength(50)]
        public string? ImageFileType { get; set; } // jpg, png, pdf, dcm...

        // Trạng thái
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Completed"; // Completed, Pending

        public DateTime ResultDate { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
