using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Healthy_System.Models
{
    public class Specialty
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên chuyên khoa không được để trống")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [StringLength(50)]
        public string Icon { get; set; } = "fa-stethoscope";

        [StringLength(255)]
        public string? ImageUrl { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation
        public ICollection<Doctor> Doctors { get; set; } = new List<Doctor>();
        public ICollection<Service> Services { get; set; } = new List<Service>();
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}
