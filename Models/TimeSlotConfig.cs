using System;
using System.ComponentModel.DataAnnotations;

namespace Healthy_System.Models
{
    public class TimeSlotConfig
    {
        [Key]
        public int Id { get; set; }

        // Tên ca: Sáng, Chiều, Tối
        [Required]
        [StringLength(50)]
        public string ShiftName { get; set; } = string.Empty;

        // Giờ bắt đầu ca (HH:mm)
        [Required]
        [StringLength(10)]
        public string StartTime { get; set; } = "08:00";

        // Giờ kết thúc ca (HH:mm)
        [Required]
        [StringLength(10)]
        public string EndTime { get; set; } = "12:00";

        // Thời lượng mỗi khung giờ (phút), ví dụ: 30
        public int SlotDurationMinutes { get; set; } = 30;

        // Số bệnh nhân tối đa mỗi khung giờ
        public int MaxPatientsPerSlot { get; set; } = 1;

        public bool IsActive { get; set; } = true;

        [StringLength(200)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
