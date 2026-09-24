using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Healthy_System.Models
{
    public class DoctorSchedule
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int DoctorId { get; set; }

        [ForeignKey("DoctorId")]
        public Doctor? Doctor { get; set; }

        [DataType(DataType.Date)]
        public DateTime WorkDate { get; set; }

        [Required]
        [StringLength(50)]
        public string TimeSlot { get; set; } = "08:00 - 08:30";

        public int MaxPatients { get; set; } = 1;

        public int BookedPatients { get; set; } = 0;

        public bool IsAvailable { get; set; } = true;

        [StringLength(255)]
        public string? Notes { get; set; }
    }
}
