using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RecruitPro.Application.DTOs.Request.Candidate
{
    public class UserInfoDto
    {
        [Required]
        [StringLength(50, MinimumLength = 4, ErrorMessage = "Username phải có từ 4 đến 50 ký tự.")]
        [RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "Username chỉ được chứa chữ cái, số, dấu chấm, gạch dưới hoặc gạch ngang.")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(100, ErrorMessage = "Họ tên không quá 100 ký tự.")]
        public string FullName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [RegularExpression(@"^0\d{9}$",
        ErrorMessage = "Số điện thoại phải có 10 số và bắt đầu bằng 0.")]
        public string Phone { get; set; }

        [Required]
        [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất {1} ký tự.")]
        [MaxLength(100, ErrorMessage = "Mật khẩu không được vượt quá {1} ký tự.")]
        public string PasswordHash { get; set; } = string.Empty;
    }
}
