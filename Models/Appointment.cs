using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Healthy_System.Models
{
    public class Appointment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string AppointmentCode { get; set; } = string.Empty;

        [Required]
        public int PatientId { get; set; }

        [ForeignKey("PatientId")]
        public User? Patient { get; set; }

        [Required]
        public int DoctorId { get; set; }

        [ForeignKey("DoctorId")]
        public Doctor? Doctor { get; set; }

        [Required]
        public int SpecialtyId { get; set; }

        [ForeignKey("SpecialtyId")]
        public Specialty? Specialty { get; set; }

        [DataType(DataType.Date)]
        public DateTime AppointmentDate { get; set; }

        [Required]
        [StringLength(50)]
        public string TimeSlot { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập lý do khám hoặc triệu chứng")]
        [StringLength(500)]
        public string ReasonForVisit { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Confirmed, Completed, Cancelled

        [StringLength(500)]
        public string? CancellationReason { get; set; }

        public DateTime? CancelledAt { get; set; }

        // US06 & US27: Clinical Examination & e-Prescription results
        [StringLength(500)]
        public string? Diagnosis { get; set; }

        [StringLength(2000)]
        public string? Prescription { get; set; }

        [StringLength(1000)]
        public string? DoctorNotes { get; set; }

        [StringLength(1000)]
        public string? LabRequest { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
