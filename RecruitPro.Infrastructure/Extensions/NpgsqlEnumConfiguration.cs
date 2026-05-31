using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using RecruitPro.Domain.Enums;

namespace RecruitPro.Infrastructure.Extensions
{
    public static class NpgsqlEnumConfiguration
    {
        public static void ConfigureEnums(NpgsqlDataSourceBuilder builder)
        {
            builder.MapEnum<JobStatus>("job_status");
            builder.MapEnum<ApplicationStatus>("application_status");
            builder.MapEnum<EmploymentType>("employment_type");
            builder.MapEnum<InterviewStatus>("interview_status");
            builder.MapEnum<MeetingType>("meeting_type");
            builder.MapEnum<NotificationType>("notification_type");
            builder.MapEnum<UserStatus>("user_status");
            builder.MapEnum<WorkMode>("work_mode");
        }
    }
}
