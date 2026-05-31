using NpgsqlTypes;

namespace RecruitPro.Domain.Enums
{
    public enum InterviewStatus
    {
        [PgName("SCHEDULED")]
        Scheduled,
        [PgName("COMPLETED")]
        Completed,
        [PgName("CANCELLED")]
        Canceled
    }
}
