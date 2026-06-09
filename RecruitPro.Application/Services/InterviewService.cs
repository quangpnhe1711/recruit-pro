using RecruitPro.Application.DTOs.Request.Interviews;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Common;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;

namespace RecruitPro.Application.Services;

public class InterviewService : IInterviewService
{
    private readonly IInterviewRepository _interviewRepository;
    private readonly IApplicationRepository _applicationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public InterviewService(
        IInterviewRepository interviewRepository,
        IApplicationRepository applicationRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _interviewRepository = interviewRepository;
        _applicationRepository = applicationRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<InterviewListResponseDto>> GetInterviewsAsync(int page, int pageSize, string? keyword, string? status, DateTime? startDate, DateTime? endDate)
    {
        InterviewStatus? parsedStatus = ParseInterviewStatus(status);
        (IReadOnlyList<Interview> interviews, int total) = await _interviewRepository.GetPagedAsync(page, pageSize, keyword, parsedStatus, startDate, endDate);

        return ApiResponse<InterviewListResponseDto>.Ok(new InterviewListResponseDto
        {
            Items = interviews.Select(interview => new InterviewListItemDto
            {
                Id = interview.Id.ToString(),
                CandidateName = interview.Application.User.FullName,
                CandidateEmail = interview.Application.User.Email,
                JobTitle = interview.Application.Job.Title,
                Interviewer = "RecruitPro HR",
                DateLabel = interview.InterviewDate.ToString("MMM dd, yyyy"),
                TimeLabel = $"{interview.InterviewDate:HH:mm} - {interview.InterviewDate.AddHours(1):HH:mm}",
                StartAt = interview.InterviewDate,
                EndAt = interview.InterviewDate.AddHours(1),
                Status = (interview.Status ?? InterviewStatus.Scheduled).ToString()
            }).ToList(),
            Meta = BuildMeta(page, pageSize, total)
        });
    }

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

    public async Task<ApiResponse<InterviewCreatedResponseDto>> CreateInterviewAsync(CreateInterviewRequest request)
    {
        if (!Guid.TryParse(request.ApplicationId, out Guid applicationGuid))
        {
            return ApiResponse<InterviewCreatedResponseDto>.BadRequest("Invalid application id.");
        }

        Domain.Entities.Application? application = await _applicationRepository.GetByIdAsync(applicationGuid);
        if (application == null)
        {
            return ApiResponse<InterviewCreatedResponseDto>.NotFound("Application not found.");
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

        await _interviewRepository.AddAsync(interview);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<InterviewCreatedResponseDto>.Created(new InterviewCreatedResponseDto
        {
            InterviewId = interview.Id.ToString()
        }, "Interview scheduled successfully");
    }

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

    private static Dictionary<string, List<int>> CloneBusySlots(Dictionary<string, List<int>> source)
    {
        return source.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToList());
    }

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

    private static ApiEnvelopeMeta BuildMeta(int page, int pageSize, int totalItems)
    {
        return new ApiEnvelopeMeta
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

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
