using System.ComponentModel.DataAnnotations;

namespace Healthy_System.Models.ViewModels
{
    public class CreateDoctorViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ và tên bác sĩ.")]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập email đăng nhập.")]
        [EmailAddress(ErrorMessage = "Địa chỉ email không đúng định dạng.")]
        [Display(Name = "Email đăng nhập")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu khởi tạo.")]
        [MinLength(6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự.")]
        [Display(Name = "Mật khẩu khởi tạo")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Display(Name = "Giới tính")]
        public string Gender { get; set; } = "Nam";

        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }

        [Display(Name = "Ảnh đại diện (URL)")]
        public string? AvatarUrl { get; set; }

        // Professional info
        [Required(ErrorMessage = "Vui lòng chọn chuyên khoa phụ trách.")]
        [Display(Name = "Chuyên khoa")]
        public int SpecialtyId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập chức danh / học vị.")]
        [Display(Name = "Chức danh / Học vị")]
        public string Title { get; set; } = "Bác sĩ Chuyên khoa";

        [Required(ErrorMessage = "Vui lòng chỉ định số phòng khám.")]
        [Display(Name = "Phòng khám")]
        public string RoomNumber { get; set; } = "P.101";

        [Range(0, 60, ErrorMessage = "Số năm kinh nghiệm từ 0 đến 60.")]
        [Display(Name = "Năm kinh nghiệm")]
        public int ExperienceYears { get; set; } = 5;

        [Range(10000, 50000000, ErrorMessage = "Phí khám từ 10.000 VNĐ đến 50.000.000 VNĐ.")]
        [Display(Name = "Phí khám (VNĐ)")]
        public decimal ConsultationFee { get; set; } = 250000;

        [Display(Name = "Tiểu sử & Quá trình công tác")]
        public string? Bio { get; set; }
    }
}
