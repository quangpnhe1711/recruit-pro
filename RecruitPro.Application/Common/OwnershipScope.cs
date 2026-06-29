using RecruitPro.Domain.Constants;
using RecruitPro.Domain.Entities;
using JobApplication = RecruitPro.Domain.Entities.Application;

namespace RecruitPro.Application.Common;

/// <summary>
/// Central ownership/authorization-scope rules for HR-facing resources (Phase 2.2).
///
/// A role grant (HR/Manager/HeadDepartment) is necessary but not sufficient: a caller may only
/// see/act on an application (and, transitively, the candidate behind it) that they own. Ownership
/// is the union of the Phase-1 snapshot fields and the live job owners:
///   recruiter side  : Application.AssignedRecruiterId | Job.CreatedBy | Job.RecruiterId
///   dept-head side  : Application.AssignedDepartmentHeadId | Job.Department.HeadUserId
///
/// Phase 2.2b correction: SystemAdmin is a system-administration role only. It does NOT bypass
/// business-data ownership for Application/Interview/Offer/Candidate data.
/// SystemAdmin-only callers are blocked at the [Authorize] layer on all business endpoints.
///
/// Phase 2.2c: <see cref="ResolveListScopeUserId"/> no longer returns null for SystemAdmin.
/// All list endpoints always scope to the caller's userId (or Guid.Empty if unavailable).
/// The null bypass (= "all records") has been removed as defense-in-depth.
///
/// IMPORTANT: the DB-side filter in ApplicationRepository.GetPagedAsync / JobRepository.GetPagedAsync
/// mirrors these predicates and must be kept in sync with them.
/// </summary>
public static class OwnershipScope
{
    public static bool IsSystemAdmin(IReadOnlyCollection<string>? roles)
        => roles != null && roles.Contains(RoleNames.SystemAdmin);

    /// <summary>
    /// The user id used to scope a list query. Always returns a non-null value: the caller's userId
    /// when available, or <see cref="Guid.Empty"/> as a sentinel that causes the repository filter to
    /// match nothing (defense-in-depth). SystemAdmin callers are blocked at the [Authorize] layer on
    /// all business endpoints (Phase 2.2c) and can no longer reach these service methods with
    /// SystemAdmin-only tokens.
    /// </summary>
    public static Guid? ResolveListScopeUserId(Guid? userId, IReadOnlyCollection<string>? roles)
        => userId ?? Guid.Empty;

    /// <summary>
    /// Whether the caller may access a single, fully-loaded application (Job and Job.Department included).
    /// Used by the detail/action guards (review detail, decision, CV, emails).
    /// SystemAdmin does NOT bypass this check — only explicit ownership qualifies.
    /// </summary>
    public static bool CanAccessApplication(JobApplication application, Guid? userId, IReadOnlyCollection<string>? roles)
    {
        if (userId is not Guid caller)
        {
            return false;
        }

        if (application.AssignedRecruiterId == caller || application.AssignedDepartmentHeadId == caller)
        {
            return true;
        }

        var job = application.Job;
        if (job == null)
        {
            return false;
        }

        return job.CreatedBy == caller
            || job.RecruiterId == caller
            || (job.Department != null && job.Department.HeadUserId == caller);
    }

    /// <summary>
    /// Whether the caller may access a single, fully-loaded job (Department included). Used to scope the
    /// per-job application list/recent endpoints to the job's owners.
    /// SystemAdmin does NOT bypass this check — only explicit ownership qualifies.
    /// </summary>
    public static bool CanAccessJob(Job job, Guid? userId, IReadOnlyCollection<string>? roles)
    {
        if (userId is not Guid caller)
        {
            return false;
        }

        return job.CreatedBy == caller
            || job.RecruiterId == caller
            || (job.Department != null && job.Department.HeadUserId == caller);
    }

    /// <summary>
    /// Whether the caller owns an interview via the Interview → Application → Job ownership chain.
    /// Used for interview list scoping (DB-side filter equivalent in
    /// <see cref="ResolveInterviewListScopeUserId"/>).
    /// </summary>
    public static bool CanAccessInterview(
        Domain.Entities.Application application,
        Guid? userId,
        IReadOnlyCollection<string>? roles)
        => CanAccessApplication(application, userId, roles);

    /// <summary>
    /// Returns the userId used to scope the interview list query. Never returns null (no unscoped
    /// access for any role). SystemAdmin-only callers cannot reach interview endpoints after Phase
    /// 2.2b [Authorize] corrections, so this always reflects a genuine business-role caller.
    /// </summary>
    public static Guid ResolveInterviewListScopeUserId(Guid? userId)
        => userId ?? Guid.Empty;
}
