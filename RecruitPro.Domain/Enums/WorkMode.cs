using NpgsqlTypes;

namespace RecruitPro.Domain.Enums
{
    public enum WorkMode
    {
        [PgName("ONSITE")]
        Onsite,
        [PgName("HYBRID")]
        Hybrid,
        [PgName("REMOTE")]
        Remote
    }
}
