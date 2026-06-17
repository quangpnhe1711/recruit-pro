using RecruitPro.Application.DTOs.Request.Interviews;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Common;
using AutoMapper;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;
using RecruitPro.Domain.Workflows;

namespace RecruitPro.Application.Services;

public class InterviewService : IInterviewService
{
    private readonly IInterviewRepository _interviewRepository;
    private readonly IApplicationRepository _applicationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the InterviewService class.
    /// </summary>
    /// <param name="interviewRepository">The <paramref name="interviewRepository"/> value.</param>
    /// <param name="applicationRepository">The <paramref name="applicationRepository"/> value.</param>
    /// <param name="userRepository">The <paramref name="userRepository"/> value.</param>
    /// <param name="unitOfWork">The <paramref name="unitOfWork"/> value.</param>
    public InterviewService(
        IInterviewRepository interviewRepository,
        IApplicationRepository applicationRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _interviewRepository = interviewRepository;
        _applicationRepository = applicationRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    /// <summary>
    /// Retrieves interviews.
    /// </summary>
    /// <param name="page">The <paramref name="page"/> value.</param>
    /// <param name="pageSize">The <paramref name="pageSize"/> value.</param>
    /// <param name="keyword">The <paramref name="keyword"/> value.</param>
    /// <param name="status">The <paramref name="status"/> value.</param>
    /// <param name="startDate">The <paramref name="startDate"/> value.</param>
    /// <param name="endDate">The <paramref name="endDate"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<InterviewListResponseDto>> GetInterviewsAsync(int page, int pageSize, string? keyword, string? status, DateTime? startDate, DateTime? endDate)
    {
        InterviewStatus? parsedStatus = ParseInterviewStatus(status);
        (IReadOnlyList<Interview> interviews, int total) = await _interviewRepository.GetPagedAsync(page, pageSize, keyword, parsedStatus, startDate, endDate);

        return ApiResponse<InterviewListResponseDto>.Ok(new InterviewListResponseDto
        {
            Items = _mapper.Map<List<InterviewListItemDto>>(interviews),
            Meta = PaginationMetaBuilder.Build(page, pageSize, total)
        });
    }

    /// <summary>
    /// Retrieves candidate interviews.
    /// </summary>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<InterviewListResponseDto>> GetCandidateInterviewsAsync(Guid userId)
    {
        IReadOnlyList<Domain.Entities.Application> applications = await _applicationRepository.GetByUserIdAsync(userId);
        List<InterviewListItemDto> items = applications
            .SelectMany(application => application.Interviews.Select(interview => interview))
            .OrderBy(interview => interview.InterviewDate)
            .Select(interview => _mapper.Map<InterviewListItemDto>(interview))
            .ToList();

        return ApiResponse<InterviewListResponseDto>.Ok(new InterviewListResponseDto
        {
            Items = items,
            Meta = PaginationMetaBuilder.Build(1, items.Count == 0 ? 10 : items.Count, items.Count)
        });
    }

    /// <summary>
    /// Retrieves schedule data.
    /// </summary>
    /// <param name="applicationId">The <paramref name="applicationId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<ScheduleDataResponseDto>> GetScheduleDataAsync(string? applicationId = null)
    {
        Domain.Entities.Application? application;
        if (!string.IsNullOrWhiteSpace(applicationId))
        {
            if (!Guid.TryParse(applicationId, out Guid applicationGuid))
            {
                return ApiResponse<ScheduleDataResponseDto>.BadRequest("Invalid application id.");
            }

            application = await _applicationRepository.GetByIdAsync(applicationGuid);
            if (application == null)
            {
                return ApiResponse<ScheduleDataResponseDto>.NotFound("Application not found.");
            }
        }
        else
        {
            application = (await _applicationRepository.GetRecentAsync(1)).FirstOrDefault();
            if (application == null)
            {
                return ApiResponse<ScheduleDataResponseDto>.NotFound("No applications available for scheduling.");
            }
        }

        IReadOnlyList<User> interviewers = await _userRepository.GetUsersInRolesAsync("HR", "Manager");
        Dictionary<string, List<int>> busySlotsByDate = await BuildBusySlotsByDateAsync();

        return ApiResponse<ScheduleDataResponseDto>.Ok(new ScheduleDataResponseDto
        {
            Candidate = new ScheduleCandidateDto
            {
                Id = application.UserId.ToString(),
                ApplicationId = application.Id.ToString(),
                JobId = application.JobId.ToString(),
                Name = application.User.FullName,
                RoleLabel = application.User.CandidateProfile?.CurrentPosition ?? string.Empty,
                AppliedFor = application.Job.Title,
                AvatarUrl = application.User.AvatarUrl
            },
            Interviewers = interviewers.Take(5).Select(user => new ScheduleInterviewerDto
            {
                Id = user.Id.ToString(),
                Name = user.FullName,
                Title = user.UserRoles.Select(userRole => userRole.Role.Name).FirstOrDefault() ?? "HR",
                AvatarUrl = user.AvatarUrl,
                // Interviewer assignment is not yet persisted, so we expose global occupied
                // slots as a conservative scheduling heuristic for all interviewers.
                BusySlotsByDate = CloneBusySlots(busySlotsByDate)
            }).ToList(),
            SlotMinutes = [540, 600, 660, 690, 780, 870, 960, 1020]
        });
    }

    /// <summary>
    /// Creates interview.
    /// </summary>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<InterviewCreatedResponseDto>> CreateInterviewAsync(CreateInterviewRequest request)
    {
        if (!Guid.TryParse(request.ApplicationId, out Guid applicationGuid))
        {
            return ApiResponse<InterviewCreatedResponseDto>.BadRequest("Invalid application id.");
        }

        Domain.Entities.Application? application = await _applicationRepository.GetTrackedByIdAsync(applicationGuid);
        if (application == null)
        {
            return ApiResponse<InterviewCreatedResponseDto>.NotFound("Application not found.");
        }

        if (application.Status != ApplicationStatus.ManagerReview &&
            application.Status != ApplicationStatus.Interview)
        {
            return ApiResponse<InterviewCreatedResponseDto>.BadRequest(
                "Interviews can only be scheduled from manager review or while an interview sequence is already active.");
        }

        if (!string.IsNullOrWhiteSpace(request.CandidateId)
            && (!Guid.TryParse(request.CandidateId, out Guid candidateGuid) || candidateGuid != application.UserId))
        {
            return ApiResponse<InterviewCreatedResponseDto>.BadRequest("Candidate does not match the selected application.");
        }

        if (!string.IsNullOrWhiteSpace(request.JobId)
            && (!Guid.TryParse(request.JobId, out Guid jobGuid) || jobGuid != application.JobId))
        {
            return ApiResponse<InterviewCreatedResponseDto>.BadRequest("Job does not match the selected application.");
        }

        if (!string.IsNullOrWhiteSpace(request.InterviewerId))
        {
            if (!Guid.TryParse(request.InterviewerId, out Guid interviewerGuid))
            {
                return ApiResponse<InterviewCreatedResponseDto>.BadRequest("Invalid interviewer id.");
            }

            User? interviewer = await _userRepository.GetByIdAsync(interviewerGuid);
            if (interviewer == null)
            {
                return ApiResponse<InterviewCreatedResponseDto>.NotFound("Interviewer not found.");
            }
        }

        Interview interview = new()
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationGuid,
            InterviewDate = request.Date.ToDateTime(TimeOnly.MinValue).AddMinutes(request.StartMinutes),
            MeetingType = request.Mode.Equals("video", StringComparison.OrdinalIgnoreCase) ? MeetingType.Online : MeetingType.Offline,
            MeetingLink = request.Mode.Equals("video", StringComparison.OrdinalIgnoreCase) ? request.LocationOrLink : null,
            Location = request.Mode.Equals("video", StringComparison.OrdinalIgnoreCase) ? null : request.LocationOrLink,
            Notes = null,
            Status = ParseInterviewStatus(request.Status) ?? InterviewStatus.Scheduled
        };

        if (ApplicationStatusWorkflow.CanTransition(application.Status, ApplicationStatus.Interview))
        {
            application.Status = ApplicationStatus.Interview;
            await _applicationRepository.UpdateAsync(application);
        }

        await _unitOfWork.BeginTransactionAsync();
        await _interviewRepository.AddAsync(interview);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.CommitAsync();

        return ApiResponse<InterviewCreatedResponseDto>.Created(
            new InterviewCreatedResponseDto
            {
                InterviewId = interview.Id.ToString()
            },
            "Interview scheduled successfully");
    }

    /// <summary>
    /// Builds busy slots by date.
    /// </summary>
    /// <returns>The operation result.</returns>
    private async Task<Dictionary<string, List<int>>> BuildBusySlotsByDateAsync()
    {
        (IReadOnlyList<Interview> interviews, _) = await _interviewRepository.GetPagedAsync(
            1,
            500,
            null,
            InterviewStatus.Scheduled,
            DbDateTime.Today,
            DbDateTime.Today.AddMonths(2));

        return interviews
            .GroupBy(interview => interview.InterviewDate.Date)
            .ToDictionary(
                group => DateOnly.FromDateTime(group.Key).ToString("yyyy-MM-dd"),
                group => group
                    .Select(interview => interview.InterviewDate.Hour * 60 + interview.InterviewDate.Minute)
                    .Distinct()
                    .OrderBy(minutes => minutes)
                    .ToList());
    }

    /// <summary>
    /// Clones busy slots.
    /// </summary>
    /// <param name="source">The <paramref name="source"/> value.</param>
    /// <returns>The operation result.</returns>
    private static Dictionary<string, List<int>> CloneBusySlots(Dictionary<string, List<int>> source)
    {
        return source.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToList());
    }

    /// <summary>
    /// Updates interview status.
    /// </summary>
    /// <param name="interviewId">The <paramref name="interviewId"/> value.</param>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<string>> UpdateInterviewStatusAsync(string interviewId, UpdateInterviewStatusRequest request)
    {
        if (!Guid.TryParse(interviewId, out Guid interviewGuid))
        {
            return ApiResponse<string>.NotFound("Interview not found.");
        }

        Interview? interview = await _interviewRepository.GetTrackedByIdAsync(interviewGuid);
        if (interview == null)
        {
            return ApiResponse<string>.NotFound("Interview not found.");
        }

        InterviewStatus? parsedStatus = ParseInterviewStatus(request.Status);
        if (!parsedStatus.HasValue)
        {
            return ApiResponse<string>.BadRequest("Invalid interview status.");
        }

        interview.Status = parsedStatus.Value;
        await _unitOfWork.SaveChangesAsync();
        return ApiResponse<string>.Ok("Interview status updated successfully");
    }

    /// <summary>
    /// Deletes interview.
    /// </summary>
    /// <param name="interviewId">The <paramref name="interviewId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<string>> DeleteInterviewAsync(string interviewId)
    {
        if (!Guid.TryParse(interviewId, out Guid interviewGuid))
        {
            return ApiResponse<string>.NotFound("Interview not found.");
        }

        Interview? interview = await _interviewRepository.GetTrackedByIdAsync(interviewGuid);
        if (interview == null)
        {
            return ApiResponse<string>.NotFound("Interview not found.");
        }

        await _interviewRepository.DeleteAsync(interview);
        await _unitOfWork.SaveChangesAsync();
        return ApiResponse<string>.Ok("Interview cancelled successfully", "Interview cancelled successfully");
    }

    /// <summary>
    /// Builds meta.
    /// </summary>
    /// <param name="page">The <paramref name="page"/> value.</param>
    /// <param name="pageSize">The <paramref name="pageSize"/> value.</param>
    /// <param name="totalItems">The <paramref name="totalItems"/> value.</param>
    /// <returns>The operation result.</returns>
    /// <summary>
    /// Parses interview status.
    /// </summary>
    /// <param name="value">The <paramref name="value"/> value.</param>
    /// <returns>The operation result.</returns>
    private static InterviewStatus? ParseInterviewStatus(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "scheduled" or "confirmed" => InterviewStatus.Scheduled,
            "completed" => InterviewStatus.Completed,
            "canceled" or "cancelled" => InterviewStatus.Canceled,
            _ => null
        };
    }

}
