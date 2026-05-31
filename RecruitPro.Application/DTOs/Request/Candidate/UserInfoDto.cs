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
        [StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters")]
        public string FullName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [RegularExpression(@"^0\d{9}$",
        ErrorMessage = "Phone number must contain 10 digits and start with 0")]
        public string Phone { get; set; }

        [Required]
        [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất {1} ký tự.")]
        [MaxLength(100, ErrorMessage = "Mật khẩu không được vượt quá {1} ký tự.")]
        public string PasswordHash { get; set; } = string.Empty;
    }
}
