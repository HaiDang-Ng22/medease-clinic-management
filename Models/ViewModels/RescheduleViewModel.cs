using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Healthy_System.Models.ViewModels
{
    public class RescheduleViewModel
    {
        public int AppointmentId { get; set; }
        public string AppointmentCode { get; set; } = string.Empty;
        public string DoctorName { get; set; } = string.Empty;
        public string SpecialtyName { get; set; } = string.Empty;
        public DateTime CurrentDate { get; set; }
        public string CurrentTimeSlot { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn ngày khám mới")]
        [DataType(DataType.Date)]
        [Display(Name = "Ngày khám mới")]
        public DateTime NewDate { get; set; } = DateTime.Today.AddDays(1);

        [Required(ErrorMessage = "Vui lòng chọn khung giờ mới")]
        [Display(Name = "Khung giờ mới")]
        public string NewTimeSlot { get; set; } = string.Empty;

        [Display(Name = "Lý do đổi lịch")]
        public string? Reason { get; set; }

        public List<string>? AvailableTimeSlots { get; set; }
    }
}
