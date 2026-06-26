using RecruitPro.Application.Common;

namespace RecruitPro.Application.Interfaces.IServices;

/// <summary>
/// Resolves the recruitment owners (candidate, recruiter, department head) of an application, applying
/// the snapshot-first resolution order in APPLICATION-OWNERSHIP-FLOW.md §2. Read-only; it never mutates
/// or publishes. Phase 2/3 uses it for review-stage authorization; Phase 6 will use it for notification
/// routing.
/// </summary>
public interface IApplicationOwnershipResolver
{
    Task<ApplicationOwnership> ResolveAsync(Guid applicationId, CancellationToken cancellationToken = default);
}
