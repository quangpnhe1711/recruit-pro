using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RecruitPro.Application.DTOs.Request.Candidate
{
    public class CandidateRegisterRequest
    {
        [Required]
        public UserInfoDto UserInfo { get; set; }
        public CandidateProfileDto? Profile { get; set; }
    }
}
