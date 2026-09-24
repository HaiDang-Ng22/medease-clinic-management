using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Healthy_System.Models.ViewModels
{
    public class BookAppointmentViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn chuyên khoa")]
        [Display(Name = "Chuyên khoa")]
        public int SpecialtyId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn bác sĩ khám")]
        [Display(Name = "Bác sĩ")]
        public int DoctorId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày khám")]
        [DataType(DataType.Date)]
        [Display(Name = "Ngày khám")]
        public DateTime AppointmentDate { get; set; } = DateTime.Today.AddDays(1);

        [Required(ErrorMessage = "Vui lòng chọn khung giờ khám")]
        [Display(Name = "Khung giờ khám")]
        public string TimeSlot { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng mô tả lý do khám hoặc triệu chứng")]
        [StringLength(500, ErrorMessage = "Mô tả tối đa 500 ký tự")]
        [Display(Name = "Lý do khám / Triệu chứng")]
        public string ReasonForVisit { get; set; } = string.Empty;

        // Data for dropdowns
        public List<Specialty>? Specialties { get; set; }
        public List<Doctor>? Doctors { get; set; }
        public List<string>? AvailableTimeSlots { get; set; }
    }
}
