using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Healthy_System.Models.ViewModels
{
    public class DoctorProfileViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        [Display(Name = "Số điện thoại")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Ảnh đại diện URL")]
        public string? AvatarUrl { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn hoặc nhập danh xưng / học vị")]
        [Display(Name = "Chức danh / Học vị")]
        public string Title { get; set; } = "Bác sĩ Chuyên khoa";

        [Required(ErrorMessage = "Vui lòng chọn chuyên khoa")]
        [Display(Name = "Chuyên khoa")]
        public int SpecialtyId { get; set; }

        [Display(Name = "Số năm kinh nghiệm")]
        [Range(0, 70, ErrorMessage = "Kinh nghiệm từ 0 đến 70 năm")]
        public int ExperienceYears { get; set; } = 5;

        [Display(Name = "Phòng khám")]
        public string RoomNumber { get; set; } = "P.101";

        [Display(Name = "Giá khám (VNĐ)")]
        [Range(0, 10000000, ErrorMessage = "Giá khám không hợp lệ")]
        public decimal ConsultationFee { get; set; } = 200000;

        [Display(Name = "Tiểu sử & Giới thiệu bản thân")]
        public string Bio { get; set; } = string.Empty;

        // Danh sách Dropdown Chuyên khoa
        public List<Specialty>? Specialties { get; set; }
    }
}