using NpgsqlTypes;

namespace RecruitPro.Domain.Enums
{
    public enum NotificationType
    {
        [PgName("SYSTEM")]
        System,
        [PgName("JOB")]
        Job,
        [PgName("INTERVIEW")]
        Interview,
        [PgName("APPLICATION")]
        Application
    }
}
