namespace RecruitPro.Application.Common;

/// <summary>
/// The resolved recruitment owners of a single application. IDs only — names/emails are looked up by
/// the caller when needed. See APPLICATION-OWNERSHIP-FLOW.md / RECRUITMENT-OWNERSHIP-MATRIX.md.
/// </summary>
public sealed class ApplicationOwnership
{
    public Guid CandidateUserId { get; init; }

    public Guid? RecruiterUserId { get; init; }

    public Guid? DepartmentHeadUserId { get; init; }
}
