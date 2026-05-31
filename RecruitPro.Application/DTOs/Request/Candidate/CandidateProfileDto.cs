using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RecruitPro.Application.DTOs.Request.Candidate
{
    public class CandidateProfileDto
    {
        [StringLength(100, ErrorMessage = "Vị trí hiện tại không quá {1} ký tự.")]
        public string? CurrentPosition { get; set; }  

        [Range(0, 50, ErrorMessage = "Số năm kinh nghiệm phải từ {1} đến {2}.")]
        public int? ExperienceYears { get; set; }

        [StringLength(200, ErrorMessage = "Thông tin học vấn không quá {1} ký tự.")]
        public string? Education { get; set; }  

        [StringLength(250, ErrorMessage = "Địa chỉ không quá {1} ký tự.")]
        public string? Address { get; set; }  

        [StringLength(1000, ErrorMessage = "Tiểu sử không vượt quá {1} ký tự.")]
        public string? Bio { get; set; }  

        [Url(ErrorMessage = "Đường dẫn GitHub không đúng định dạng URL.")]
        public string? GitHubUrl { get; set; }  

        [Url(ErrorMessage = "Đường dẫn LinkedIn không đúng định dạng URL.")]
        public string? LinkedInUrl { get; set; }  
    }
}
