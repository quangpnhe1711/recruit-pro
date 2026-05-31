using NpgsqlTypes;

namespace RecruitPro.Domain.Enums
{
    public enum MeetingType
    {
        [PgName("ONLINE")]
        Online,
        [PgName("OFFLINE")]
        Offline
    }
}
