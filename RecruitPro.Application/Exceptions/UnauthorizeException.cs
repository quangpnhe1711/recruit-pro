using RecruitPro.Application.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RecruitPro.Application.Exceptions
{
    public class UnauthorizeException : BaseException
    {
        public UnauthorizeException(string message) : base("Authorization failed", HttpStatusCodeConstants.Unauthorized)
        {
        }
    }
}
