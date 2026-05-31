using NpgsqlTypes;

namespace RecruitPro.Domain.Enums
{
    public enum EmploymentType
    {
        [PgName("FULL_TIME")]
        FullTime,
        [PgName("PART_TIME")]
        PartTime,
        [PgName("INTERNSHIP")]
        Internship,
        [PgName("CONTRACT")]
        Contract
    }
}
