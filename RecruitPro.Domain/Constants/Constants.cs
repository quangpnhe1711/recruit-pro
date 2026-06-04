using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RecruitPro.Domain.Enums;

namespace RecruitPro.Domain.Constants
{
    public static class Constants
    {
        public static readonly JobStatus[] NOT_SHOW_JOB_STATUS =
        {
        JobStatus.Rejected,
        JobStatus.PendingApproval,
        JobStatus.Draft
        };
    }
}
