using NpgsqlTypes;

namespace RecruitPro.Domain.Enums
{
    public enum JobStatus
    {
        [PgName("DRAFT")]
        Draft,
        [PgName("PENDING_APPROVAL")]
        PendingApproval,
        [PgName("APPROVED")]
        Approved,
        [PgName("CLOSED")]
        Closed,
        [PgName("REJECTED")]
        Rejected
    }
}
