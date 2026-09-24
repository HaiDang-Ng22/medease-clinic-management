using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Healthy_System.Models
{
    public class Doctor
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required]
        public int SpecialtyId { get; set; }

        [ForeignKey("SpecialtyId")]
        public Specialty? Specialty { get; set; }

        [StringLength(100)]
        public string Title { get; set; } = "Bác sĩ Chuyên khoa";

        public string Bio { get; set; } = string.Empty;

        public int ExperienceYears { get; set; } = 5;

        [StringLength(50)]
        public string RoomNumber { get; set; } = "P.101";

        [Column(TypeName = "decimal(18,2)")]
        public decimal ConsultationFee { get; set; } = 200000;

        public double Rating { get; set; } = 5.0;

        public int ReviewCount { get; set; } = 0;

        // Navigation
        public ICollection<DoctorSchedule> Schedules { get; set; } = new List<DoctorSchedule>();
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}
