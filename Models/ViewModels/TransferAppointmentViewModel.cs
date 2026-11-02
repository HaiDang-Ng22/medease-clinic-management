using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Healthy_System.Models.ViewModels
{
    public class TransferAppointmentViewModel
    {
        public int AppointmentId { get; set; }
        public string AppointmentCode { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string? PatientPhone { get; set; }
        public string? ReasonForVisit { get; set; }
        public string CurrentStatus { get; set; } = string.Empty;

        // Thông tin hiện tại
        public int CurrentDoctorId { get; set; }
        public string CurrentDoctorName { get; set; } = string.Empty;
        public string? CurrentDoctorRoom { get; set; }
        public int CurrentSpecialtyId { get; set; }
        public string CurrentSpecialtyName { get; set; } = string.Empty;
        public DateTime CurrentDate { get; set; }
        public string CurrentTimeSlot { get; set; } = string.Empty;

        // Thông tin chuyển đổi mới
        [Required(ErrorMessage = "Vui lòng chọn chuyên khoa")]
        [Display(Name = "Chuyên khoa")]
        public int NewSpecialtyId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn bác sĩ tiếp nhận")]
        [Display(Name = "Bác sĩ mới")]
        public int NewDoctorId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày khám mới")]
        [DataType(DataType.Date)]
        [Display(Name = "Ngày khám mới")]
        public DateTime NewDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Vui lòng chọn khung giờ khám mới")]
        [Display(Name = "Khung giờ mới")]
        public string NewTimeSlot { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập lý do chuyển đổi lịch hẹn")]
        [StringLength(500, ErrorMessage = "Lý do không quá 500 ký tự")]
        [Display(Name = "Lý do chuyển đổi")]
        public string TransferReason { get; set; } = string.Empty;

        [Display(Name = "Gửi thông báo hệ thống đến bệnh nhân")]
        public bool NotifyPatient { get; set; } = true;

        [Display(Name = "Gửi thông báo đến bác sĩ liên quan")]
        public bool NotifyDoctors { get; set; } = true;

        // Danh sách dữ liệu hỗ trợ view
        public List<SelectListItem> Specialties { get; set; } = new();
        public List<SelectListItem> Doctors { get; set; } = new();
        public List<string> AvailableSlots { get; set; } = new();
    }
}
