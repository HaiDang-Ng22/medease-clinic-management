using System;
using System.ComponentModel.DataAnnotations;

namespace Healthy_System.Models.ViewModels
{
    public class CancelAppointmentViewModel
    {
        public int AppointmentId { get; set; }
        public string AppointmentCode { get; set; } = string.Empty;
        public string DoctorName { get; set; } = string.Empty;
        public DateTime AppointmentDate { get; set; }
        public string TimeSlot { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng cung cấp lý do hủy lịch hẹn")]
        [StringLength(300, ErrorMessage = "Lý do tối đa 300 ký tự")]
        [Display(Name = "Lý do hủy lịch")]
        public string CancellationReason { get; set; } = string.Empty;
    }
}
