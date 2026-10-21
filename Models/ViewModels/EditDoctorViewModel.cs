using System.ComponentModel.DataAnnotations;

namespace Healthy_System.Models.ViewModels
{
    public class EditDoctorViewModel
    {
        public int Id { get; set; } // DoctorId
        public int UserId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Email đăng nhập")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Số điện thoại")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Giới tính")]
        public string? Gender { get; set; }

        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }

        [Display(Name = "Ảnh đại diện (URL)")]
        public string? AvatarUrl { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn chuyên khoa.")]
        [Display(Name = "Chuyên khoa")]
        public int SpecialtyId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập chức danh.")]
        [Display(Name = "Chức danh / Học vị")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chỉ định số phòng khám.")]
        [Display(Name = "Phòng khám")]
        public string RoomNumber { get; set; } = string.Empty;

        [Range(0, 60, ErrorMessage = "Số năm kinh nghiệm từ 0 đến 60.")]
        [Display(Name = "Năm kinh nghiệm")]
        public int ExperienceYears { get; set; }

        [Range(10000, 50000000, ErrorMessage = "Phí khám từ 10.000 VNĐ đến 50.000.000 VNĐ.")]
        [Display(Name = "Phí khám (VNĐ)")]
        public decimal ConsultationFee { get; set; }

        [Display(Name = "Tiểu sử & Quá trình công tác")]
        public string? Bio { get; set; }
    }
}
