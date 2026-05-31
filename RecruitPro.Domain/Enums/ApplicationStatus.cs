using NpgsqlTypes;

namespace RecruitPro.Domain.Enums
{
    public enum ApplicationStatus
    {
        [PgName("PENDING")]
        Pending,
        [PgName("REVIEWING")]
        Reviewing,
        [PgName("INTERVIEWING")]
        Interviewing,
        [PgName("MANAGER_REVIEW")]
        ManagerReview,
        [PgName("ACCEPTED")]
        Accepted,
        [PgName("REJECTED")]
        Rejected,
    }
}
