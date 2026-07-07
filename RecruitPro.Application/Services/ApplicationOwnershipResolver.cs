using RecruitPro.Application.Common;
using RecruitPro.Application.Exceptions;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.Application.Services;

/// <summary>
/// Default <see cref="IApplicationOwnershipResolver"/>. Loads the application (with its Job + Department)
/// and applies the snapshot-first resolution order. <see cref="Resolve"/> is exposed as a pure helper so
/// callers that already hold a loaded <see cref="Domain.Entities.Application"/> (e.g. the review-stage
/// guard) can resolve ownership without a second round-trip.
/// </summary>
public sealed class ApplicationOwnershipResolver : IApplicationOwnershipResolver
{
    private readonly IApplicationRepository _applicationRepository;

    public ApplicationOwnershipResolver(IApplicationRepository applicationRepository)
    {
        _applicationRepository = applicationRepository;
    }

    public async Task<ApplicationOwnership> ResolveAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        Domain.Entities.Application? application = await _applicationRepository.GetByIdAsync(applicationId);
        if (application == null)
        {
            throw new BusinessAppException(ErrorCodes.ApplicationNotFound, 404);
        }

        return Resolve(application);
    }

    /// <summary>
    /// Pure resolution from an already-loaded application. Requires <c>application.Job</c> (and its
    /// <c>Department</c>) to be loaded for the fallbacks to apply.
    ///   Recruiter      = AssignedRecruiterId ?? Job.RecruiterId ?? Job.CreatedBy
    ///   DepartmentHead = AssignedDepartmentHeadId ?? Job.Department.HeadUserId ?? Job.ApprovedBy
    /// </summary>
    public static ApplicationOwnership Resolve(Domain.Entities.Application application)
    {
        Domain.Entities.Job? job = application.Job;

        Guid? recruiter = application.AssignedRecruiterId
            ?? job?.RecruiterId
            ?? job?.CreatedBy;

        Guid? departmentHead = application.AssignedDepartmentHeadId
            ?? job?.Department?.HeadUserId
            ?? job?.ApprovedBy;

        return new ApplicationOwnership
        {
            CandidateUserId = application.UserId,
            RecruiterUserId = recruiter,
            DepartmentHeadUserId = departmentHead
        };
    }
}
