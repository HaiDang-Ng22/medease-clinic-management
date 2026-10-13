using System.ComponentModel.DataAnnotations;

namespace Healthy_System.Models.ViewModels
{
    public class ReviewViewModel
    {
        public int AppointmentId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string SpecialtyName { get; set; } = string.Empty;
        public string AppointmentDate { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn số sao đánh giá")]
        [Range(1, 5, ErrorMessage = "Đánh giá từ 1 đến 5 sao")]
        [Display(Name = "Điểm đánh giá")]
        public int Rating { get; set; } = 5;

        [StringLength(1000, ErrorMessage = "Nhận xét tối đa 1000 ký tự")]
        [Display(Name = "Nhận xét của bạn (Không bắt buộc)")]
        public string? Comment { get; set; }
    }
}