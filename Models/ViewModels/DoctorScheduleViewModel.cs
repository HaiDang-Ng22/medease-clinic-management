using System;
using System.ComponentModel.DataAnnotations;

namespace Healthy_System.Models.ViewModels
{
    public class DoctorScheduleViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn ngày làm việc")]
        [DataType(DataType.Date)]
        [Display(Name = "Ngày làm việc")]
        public DateTime WorkDate { get; set; } = DateTime.Today.AddDays(1);

        [Required(ErrorMessage = "Vui lòng chọn khung giờ")]
        [Display(Name = "Khung giờ")]
        public string TimeSlot { get; set; } = "08:00 - 08:30";

        [Display(Name = "Số bệnh nhân tối đa")]
        [Range(1, 10, ErrorMessage = "Tối đa từ 1 đến 10 bệnh nhân mỗi ca")]
        public int MaxPatients { get; set; } = 1;

        [Display(Name = "Ghi chú")]
        public string? Notes { get; set; }
    }
}