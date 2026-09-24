using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Healthy_System.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        [StringLength(20)]
        public string? PhoneNumber { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [StringLength(10)]
        public string Gender { get; set; } = "Nam";

        [StringLength(255)]
        public string? Address { get; set; }

        [StringLength(255)]
        public string? AvatarUrl { get; set; }

        [Required]
        [StringLength(20)]
        public string Role { get; set; } = "Patient"; // Patient, Doctor, Receptionist, LabTech, Accountant, Admin

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Doctor? DoctorProfile { get; set; }
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }
}
