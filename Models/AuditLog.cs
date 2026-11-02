using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Healthy_System.Models
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        // Người thực hiện hành động (null nếu là hệ thống)
        public int? UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        // Tên người dùng lưu tại thời điểm ghi log (phòng khi xóa user)
        [StringLength(100)]
        public string ActorName { get; set; } = "Hệ thống";

        [StringLength(50)]
        public string ActorRole { get; set; } = "System";

        // Loại hành động: Create, Update, Delete, Login, Logout, Cancel, Confirm...
        [Required]
        [StringLength(50)]
        public string Action { get; set; } = string.Empty;

        // Bảng / đối tượng bị tác động: User, Appointment, Doctor, DoctorSchedule...
        [StringLength(100)]
        public string EntityName { get; set; } = string.Empty;

        // Mã định danh của bản ghi bị tác động
        [StringLength(100)]
        public string? EntityId { get; set; }

        // Mô tả chi tiết hành động
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        // Địa chỉ IP của người dùng
        [StringLength(50)]
        public string? IpAddress { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
