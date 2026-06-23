using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;

namespace RecruitPro.Application.Interfaces.IServices;

public interface INotificationEventService
{
    Task PublishNewApplicationReceivedAsync(RecruitPro.Domain.Entities.Application application);
    Task PublishApplicationStatusChangedAsync(RecruitPro.Domain.Entities.Application application, ApplicationStatus previousStatus);
    Task PublishInterviewScheduledAsync(RecruitPro.Domain.Entities.Application application, Interview interview, Guid? interviewerId);
    Task PublishCandidateScoreReadyAsync(RecruitPro.Domain.Entities.Application application);
}
