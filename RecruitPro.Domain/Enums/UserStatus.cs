using NpgsqlTypes;

namespace RecruitPro.Domain.Enums
{
    public enum UserStatus
    {
        [PgName("ACTIVE")]
        Active,
        [PgName("INACTIVE")]
        Inactive,
        [PgName("BLOCKED")]
        Blocked
    }
}
