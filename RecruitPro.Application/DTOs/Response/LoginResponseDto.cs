using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RecruitPro.Application.DTOs.Response
{
    public class LoginResponseDto
    {
        public string AccessToken { get; set; } = null!;

        public UserDto User { get; set; } = null!;
    }
}
