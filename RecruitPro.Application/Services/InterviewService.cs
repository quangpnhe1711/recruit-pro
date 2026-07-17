using RecruitPro.Application.DTOs.Request.Interviews;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Common;
using AutoMapper;
using Microsoft.Extensions.Logging;
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
    private readonly INotificationEventService _notificationEventService;
    private readonly ILogger<InterviewService> _logger;
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
        INotificationEventService notificationEventService,
        ILogger<InterviewService> logger,
        IMapper mapper)
    {
        _interviewRepository = interviewRepository;
        _applicationRepository = applicationRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _notificationEventService = notificationEventService;
        _logger = logger;
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
    public async Task<ApiResponse<InterviewListResponseDto>> GetInterviewsAsync(int page, int pageSize, string? keyword, string? status, DateTime? startDate, DateTime? endDate, Guid? callerUserId, IReadOnlyCollection<string> callerRoles)
    {
        InterviewStatus? parsedStatus = ParseInterviewStatus(status);
        // Phase 2.2b: filter interviews DB-side to only those the caller owns via the
        // Interview -> Application -> Job ownership chain. Never unscoped (Guid.Empty means no records).
        Guid scopeUserId = OwnershipScope.ResolveInterviewListScopeUserId(callerUserId);
        (IReadOnlyList<Interview> interviews, int total) = await _interviewRepository.GetPagedAsync(page, pageSize, keyword, parsedStatus, startDate, endDate, scopeUserId);

        List<InterviewListItemDto> items = _mapper.Map<List<InterviewListItemDto>>(interviews);

        // Scorecard summary is internal-only: it is enriched here (HR/Manager list) instead of in the
        // shared AutoMapper profile so the candidate list can never leak evaluation data.
        for (int index = 0; index < interviews.Count; index++)
        {
            InterviewEvaluation? evaluation = interviews[index].Evaluation;
            if (evaluation != null)
            {
                items[index].EvaluationOverallScore = evaluation.OverallScore;
                items[index].EvaluationRecommendation = evaluation.Recommendation.ToString();
            }
        }

        return ApiResponse<InterviewListResponseDto>.Ok(new InterviewListResponseDto
        {
            Items = items,
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
        if (string.IsNullOrWhiteSpace(applicationId))
        {
            return ApiResponse<ScheduleDataResponseDto>.BadRequest(ErrorCodes.InvalidInput);
        }

        if (!Guid.TryParse(applicationId, out Guid applicationGuid))
        {
            return ApiResponse<ScheduleDataResponseDto>.BadRequest(ErrorCodes.InvalidInput);
        }

        Domain.Entities.Application? application = await _applicationRepository.GetByIdAsync(applicationGuid);
        if (application == null)
        {
            return ApiResponse<ScheduleDataResponseDto>.NotFound(ErrorCodes.ApplicationNotFound);
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
            return ApiResponse<InterviewCreatedResponseDto>.BadRequest(ErrorCodes.InvalidInput);
        }

        // BUG-UAT-007: StartMinutes is minutes-from-midnight and is used to build a DateTime below
        // (hour = StartMinutes / 60). An out-of-range value threw ArgumentOutOfRangeException → 500.
        // Reject it as a 400 before constructing the time.
        if (request.StartMinutes < 0 || request.StartMinutes > 1439)
        {
            return ApiResponse<InterviewCreatedResponseDto>.BadRequest(ErrorCodes.InvalidInput);
        }

        Domain.Entities.Application? application = await _applicationRepository.GetTrackedByIdAsync(applicationGuid);
        if (application == null)
        {
            return ApiResponse<InterviewCreatedResponseDto>.NotFound(ErrorCodes.ApplicationNotFound);
        }

        // INV-008: an interview is only valid/actionable when the application is at the ManagerReview
        // hand-off or already in the Interview stage. Scheduling for any other stage is an invalid
        // business state (422), not a malformed request (400).
        if (application.Status != ApplicationStatus.ManagerReview &&
            application.Status != ApplicationStatus.Interview)
        {
            return ApiResponse<InterviewCreatedResponseDto>.UnprocessableEntity(ErrorCodes.InterviewNotActionable);
        }

        if (!string.IsNullOrWhiteSpace(request.CandidateId)
            && (!Guid.TryParse(request.CandidateId, out Guid candidateGuid) || candidateGuid != application.UserId))
        {
            return ApiResponse<InterviewCreatedResponseDto>.BadRequest(ErrorCodes.InvalidInput);
        }

        if (!string.IsNullOrWhiteSpace(request.JobId)
            && (!Guid.TryParse(request.JobId, out Guid jobGuid) || jobGuid != application.JobId))
        {
            return ApiResponse<InterviewCreatedResponseDto>.BadRequest(ErrorCodes.InvalidInput);
        }

        Guid? interviewerGuid = null;
        if (!string.IsNullOrWhiteSpace(request.InterviewerId))
        {
            if (!Guid.TryParse(request.InterviewerId, out Guid parsedInterviewerGuid))
            {
                return ApiResponse<InterviewCreatedResponseDto>.BadRequest(ErrorCodes.InvalidInput);
            }

            User? interviewer = await _userRepository.GetByIdAsync(parsedInterviewerGuid);
            if (interviewer == null)
            {
                return ApiResponse<InterviewCreatedResponseDto>.NotFound(ErrorCodes.UserNotFound);
            }

            interviewerGuid = parsedInterviewerGuid;
        }

        Interview interview = new()
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationGuid,
            InterviewDate = DateTime.SpecifyKind(
                new DateTime(request.Date.Year, request.Date.Month, request.Date.Day,
                    request.StartMinutes / 60, request.StartMinutes % 60, 0),
                DateTimeKind.Unspecified),
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

        // Best-effort, post-commit: notify candidate + recruiter + department head + interviewer.
        // A publish failure must not turn a committed schedule into a 500.
        try
        {
            await _notificationEventService.PublishInterviewScheduledAsync(application, interview, interviewerGuid);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Interview {InterviewId} was scheduled but the interview-scheduled notification failed to publish.",
                interview.Id);
        }

        return ApiResponse<InterviewCreatedResponseDto>.Created(
            new InterviewCreatedResponseDto
            {
                InterviewId = interview.Id.ToString()
            },
            "Lên lịch phỏng vấn thành công");
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
            DbDateTime.Today.AddMonths(2),
            Guid.Empty);

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
            return ApiResponse<string>.NotFound(ErrorCodes.InterviewNotFound);
        }

        Interview? interview = await _interviewRepository.GetTrackedByIdAsync(interviewGuid);
        if (interview == null)
        {
            return ApiResponse<string>.NotFound(ErrorCodes.InterviewNotFound);
        }

        InterviewStatus? parsedStatus = ParseInterviewStatus(request.Status);
        if (!parsedStatus.HasValue)
        {
            return ApiResponse<string>.BadRequest(ErrorCodes.InvalidInput);
        }

        bool becameCompleted = parsedStatus.Value == InterviewStatus.Completed
            && interview.Status != InterviewStatus.Completed;
        interview.Status = parsedStatus.Value;
        await _unitOfWork.SaveChangesAsync();

        // interview_completed: internal owners now know a post-interview decision is available. The
        // tracked interview is loaded without its Application, so resolve the application for routing.
        // Best-effort, post-commit — a publish failure must not fail the status update.
        if (becameCompleted)
        {
            try
            {
                Domain.Entities.Application? application = await _applicationRepository.GetByIdAsync(interview.ApplicationId);
                if (application != null)
                {
                    await _notificationEventService.PublishInterviewCompletedAsync(application, interview);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Interview {InterviewId} was completed but the interview-completed notification failed to publish.",
                    interview.Id);
            }
        }

        return ApiResponse<string>.Ok("Cập nhật trạng thái phỏng vấn thành công");
    }

    /// <summary>
    /// Records the candidate's attendance confirmation for their own Scheduled interview
    /// (interview:confirm-own). Idempotent: confirming an already-confirmed interview succeeds
    /// without changing the original confirmation time.
    /// </summary>
    /// <param name="interviewId">The <paramref name="interviewId"/> value.</param>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<string>> ConfirmCandidateInterviewAsync(string interviewId, Guid userId)
    {
        if (!Guid.TryParse(interviewId, out Guid interviewGuid))
        {
            return ApiResponse<string>.NotFound(ErrorCodes.InterviewNotFound);
        }

        Interview? interview = await _interviewRepository.GetTrackedByIdAsync(interviewGuid);
        if (interview == null)
        {
            return ApiResponse<string>.NotFound(ErrorCodes.InterviewNotFound);
        }

        // Ownership: only the candidate who owns the underlying application may confirm.
        Domain.Entities.Application? application = await _applicationRepository.GetByIdAsync(interview.ApplicationId);
        if (application == null || application.UserId != userId)
        {
            return ApiResponse<string>.Forbidden(ErrorCodes.Forbidden);
        }

        // Only a Scheduled interview is confirmable; Completed/Canceled are terminal for attendance.
        if ((interview.Status ?? InterviewStatus.Scheduled) != InterviewStatus.Scheduled)
        {
            return ApiResponse<string>.UnprocessableEntity(ErrorCodes.InterviewNotActionable);
        }

        if (interview.CandidateConfirmedAt == null)
        {
            interview.CandidateConfirmedAt = DbDateTime.Now;
            await _unitOfWork.SaveChangesAsync();
        }

        return ApiResponse<string>.Ok("Đã xác nhận tham dự phỏng vấn.", "Đã xác nhận tham dự phỏng vấn.");
    }

    /// <summary>
    /// Retrieves the post-interview scorecard for an interview (internal HR/Manager view).
    /// </summary>
    /// <param name="interviewId">The <paramref name="interviewId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<InterviewEvaluationDto>> GetInterviewEvaluationAsync(string interviewId)
    {
        if (!Guid.TryParse(interviewId, out Guid interviewGuid))
        {
            return ApiResponse<InterviewEvaluationDto>.NotFound(ErrorCodes.InterviewNotFound);
        }

        InterviewEvaluation? evaluation = await _interviewRepository.GetEvaluationByInterviewIdAsync(interviewGuid);
        if (evaluation == null)
        {
            return ApiResponse<InterviewEvaluationDto>.NotFound(ErrorCodes.EntityNotFound);
        }

        return ApiResponse<InterviewEvaluationDto>.Ok(BuildEvaluationDto(evaluation));
    }

    /// <summary>
    /// Creates or updates the post-interview scorecard. Only a Completed interview can be
    /// evaluated (the scorecard records what happened in the interview, so it gates on
    /// completion the same way Interview → Offer does — BR-WF-005).
    /// </summary>
    /// <param name="interviewId">The <paramref name="interviewId"/> value.</param>
    /// <param name="evaluatorId">The <paramref name="evaluatorId"/> value.</param>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<InterviewEvaluationDto>> UpsertInterviewEvaluationAsync(string interviewId, Guid? evaluatorId, UpsertInterviewEvaluationRequest request)
    {
        if (!Guid.TryParse(interviewId, out Guid interviewGuid))
        {
            return ApiResponse<InterviewEvaluationDto>.NotFound(ErrorCodes.InterviewNotFound);
        }

        Interview? interview = await _interviewRepository.GetTrackedByIdAsync(interviewGuid);
        if (interview == null)
        {
            return ApiResponse<InterviewEvaluationDto>.NotFound(ErrorCodes.InterviewNotFound);
        }

        if ((interview.Status ?? InterviewStatus.Scheduled) != InterviewStatus.Completed)
        {
            return ApiResponse<InterviewEvaluationDto>.UnprocessableEntity(ErrorCodes.InterviewNotActionable);
        }

        int[] criteriaScores =
        [
            request.TechnicalScore,
            request.CommunicationScore,
            request.ProblemSolvingScore,
            request.CultureFitScore
        ];
        if (criteriaScores.Any(score => score < 1 || score > 5))
        {
            return ApiResponse<InterviewEvaluationDto>.BadRequest(ErrorCodes.InvalidInput);
        }

        if (!Enum.TryParse(request.Recommendation, ignoreCase: true, out InterviewRecommendation recommendation))
        {
            return ApiResponse<InterviewEvaluationDto>.BadRequest(ErrorCodes.InvalidInput);
        }

        InterviewEvaluation? evaluation = await _interviewRepository.GetTrackedEvaluationByInterviewIdAsync(interviewGuid);
        bool isNew = evaluation == null;
        evaluation ??= new InterviewEvaluation
        {
            Id = Guid.NewGuid(),
            InterviewId = interviewGuid,
            CreatedAt = DbDateTime.Now
        };

        evaluation.EvaluatorId = evaluatorId;
        evaluation.TechnicalScore = request.TechnicalScore;
        evaluation.CommunicationScore = request.CommunicationScore;
        evaluation.ProblemSolvingScore = request.ProblemSolvingScore;
        evaluation.CultureFitScore = request.CultureFitScore;
        // 4 criteria x 1..5 → sum 4..20 → 0..100 scale.
        evaluation.OverallScore = (int)Math.Round(criteriaScores.Sum() / 20.0 * 100.0);
        evaluation.Recommendation = recommendation;
        evaluation.Strengths = string.IsNullOrWhiteSpace(request.Strengths) ? null : request.Strengths.Trim();
        evaluation.Concerns = string.IsNullOrWhiteSpace(request.Concerns) ? null : request.Concerns.Trim();
        evaluation.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        evaluation.UpdatedAt = DbDateTime.Now;

        if (isNew)
        {
            await _interviewRepository.AddEvaluationAsync(evaluation);
        }

        await _unitOfWork.SaveChangesAsync();

        InterviewEvaluation? savedEvaluation = await _interviewRepository.GetEvaluationByInterviewIdAsync(interviewGuid);
        return ApiResponse<InterviewEvaluationDto>.Ok(
            BuildEvaluationDto(savedEvaluation ?? evaluation),
            "Đã lưu đánh giá phỏng vấn.");
    }

    /// <summary>
    /// Builds the evaluation dto.
    /// </summary>
    /// <param name="evaluation">The <paramref name="evaluation"/> value.</param>
    /// <returns>The operation result.</returns>
    private static InterviewEvaluationDto BuildEvaluationDto(InterviewEvaluation evaluation)
    {
        return new InterviewEvaluationDto
        {
            InterviewId = evaluation.InterviewId.ToString(),
            EvaluatorId = evaluation.EvaluatorId?.ToString(),
            EvaluatorName = evaluation.Evaluator?.FullName,
            TechnicalScore = evaluation.TechnicalScore,
            CommunicationScore = evaluation.CommunicationScore,
            ProblemSolvingScore = evaluation.ProblemSolvingScore,
            CultureFitScore = evaluation.CultureFitScore,
            OverallScore = evaluation.OverallScore,
            Recommendation = evaluation.Recommendation.ToString(),
            Strengths = evaluation.Strengths,
            Concerns = evaluation.Concerns,
            Notes = evaluation.Notes,
            CreatedAt = evaluation.CreatedAt,
            UpdatedAt = evaluation.UpdatedAt
        };
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
            return ApiResponse<string>.NotFound(ErrorCodes.InterviewNotFound);
        }

        Interview? interview = await _interviewRepository.GetTrackedByIdAsync(interviewGuid);
        if (interview == null)
        {
            return ApiResponse<string>.NotFound(ErrorCodes.InterviewNotFound);
        }

        await _interviewRepository.DeleteAsync(interview);
        await _unitOfWork.SaveChangesAsync();
        return ApiResponse<string>.Ok("Đã hủy lịch phỏng vấn.", "Đã hủy lịch phỏng vấn.");
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
