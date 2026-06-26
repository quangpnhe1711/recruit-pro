namespace RecruitPro.Domain.Constants;

/// <summary>
/// Canonical role name strings as seeded in <c>init.sql</c> and used in <c>[Authorize(Roles = ...)]</c>
/// and role-membership queries. These are the existing role names — they are intentionally NOT renamed
/// (ManagerReview = the DepartmentHeadReview business stage; Manager / HeadDepartment kept as-is).
/// </summary>
public static class RoleNames
{
    public const string Candidate = "Candidate";
    public const string Hr = "HR";
    public const string Manager = "Manager";
    public const string HeadDepartment = "HeadDepartment";
    public const string SystemAdmin = "SystemAdmin";
}
