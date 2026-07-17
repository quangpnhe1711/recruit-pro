using Microsoft.Extensions.Logging;
using RecruitPro.Application.Common;
using RecruitPro.Application.DTOs.Request.Applications;
using RecruitPro.Application.DTOs.Request.Interviews;
using RecruitPro.Application.DTOs.Request.Offers;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Application.Services;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;

namespace RecruitPro.Tests;

/// <summary>
/// Workflow-correctness phase (T-WF-001..015): Head Review hand-off date, mandatory interview
/// scheduling/completion before Offer/Reject, and the email-gated Offer/Reject transitions. Unit-level
/// against the services with mocked repositories and a mocked <see cref="IEmailService"/>.
/// </summary>
public sealed class WorkflowDecisionEmailTests
{
    // Stable ID used as both actor and AssignedRecruiterId so workflow tests pass ownership checks.
    private static readonly Guid WorkflowActorId = Guid.Parse("A0000000-0000-0000-0000-000000000001");
    // ---- T-WF-001: Screening -> ManagerReview records the Head Review hand-off date ----
    [Fact]
    public async Task ScreeningToManagerReview_SetsDepartmentHeadReviewRequestedAt()
    {
        Domain.Entities.Application application = BuildApplication(ApplicationStatus.Screening);
        application.DepartmentHeadReviewRequestedAt.Should().BeNull();
        ApplicationService service = BuildApplicationService(application);

        var response = await service.UpdateApplicationDecisionAsync(
            application.Id.ToString(), Guid.NewGuid(), new UpdateApplicationDecisionRequest { TargetStatus = "ManagerReview" });

        response.StatusCode.Should().Be(200);
        application.Status.Should().Be(ApplicationStatus.ManagerReview);
        application.DepartmentHeadReviewRequestedAt.Should().NotBeNull();
    }

    // ---- T-WF-002: the manager review queue DTO carries the Head Review hand-off date ----
    [Fact]
    public async Task ManagerReviewQueue_UsesDepartmentHeadReviewRequestedAt()
    {
        DateTime requestedAt = new(2026, 6, 5, 10, 15, 0);
        Domain.Entities.Application application = BuildApplication(ApplicationStatus.ManagerReview);
        application.AppliedAt = new DateTime(2026, 6, 1);
        application.DepartmentHeadReviewRequestedAt = requestedAt;

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(repository => repository.GetManagerReviewQueueAsync(It.IsAny<string?>()))
            .ReturnsAsync([application]);

        ApplicationService service = BuildApplicationService(application, applicationRepository);

        var response = await service.GetManagerReviewQueueAsync(1, 10, null);

        response.StatusCode.Should().Be(200);
        response.Data!.Items.Should().ContainSingle();
        response.Data.Items[0].DepartmentHeadReviewRequestedAt.Should().Be(requestedAt);
        response.Data.Items[0].AppliedAt.Should().Be(new DateTime(2026, 6, 1));
    }

    // ---- T-WF-003: advancing ManagerReview -> Interview does not create an offer ----
    [Fact]
    public async Task ManagerReviewToInterview_DoesNotCreateOffer()
    {
        Guid headId = Guid.NewGuid();
        Domain.Entities.Application application = BuildApplication(ApplicationStatus.ManagerReview, assignedHeadId: headId);
        ApplicationService service = BuildApplicationService(application);

        var response = await service.UpdateApplicationDecisionAsync(
            application.Id.ToString(), headId, new UpdateApplicationDecisionRequest { TargetStatus = "Interview" });

        response.StatusCode.Should().Be(200);
        application.Status.Should().Be(ApplicationStatus.Interview);
        application.Offer.Should().BeNull();
        response.Data!.OfferStatus.Should().BeNull();
    }

    // ---- T-WF-004: Interview -> Offer blocked when no interview scheduled ----
    [Fact]
    public async Task InterviewToOffer_Blocked_WhenNoInterviewScheduled()
    {
        Domain.Entities.Application application = BuildApplication(ApplicationStatus.Interview);
        OfferService service = BuildOfferService(application, out _);

        var response = await service.SendOfferAsync(application.Id.ToString(), WorkflowActorId, new[] { "HR" }, BuildOfferRequest());

        response.StatusCode.Should().Be(422);
        response.ErrorCode.Should().Be(ErrorCodes.InterviewRequired);
        application.Status.Should().Be(ApplicationStatus.Interview);
    }

    // ---- T-WF-005: Interview -> Rejected blocked when no interview scheduled ----
    [Fact]
    public async Task InterviewToReject_Blocked_WhenNoInterviewScheduled()
    {
        Domain.Entities.Application application = BuildApplication(ApplicationStatus.Interview);
        ApplicationService service = BuildApplicationService(application);

        var response = await service.SendRejectionEmailAsync(
            application.Id.ToString(), Guid.NewGuid(), BuildRejectionRequest());

        response.StatusCode.Should().Be(422);
        response.ErrorCode.Should().Be(ErrorCodes.InterviewRequired);
        application.Status.Should().Be(ApplicationStatus.Interview);
    }

    // ---- T-WF-006: Interview -> Offer blocked when interview scheduled but not completed ----
    [Fact]
    public async Task InterviewToOffer_Blocked_WhenInterviewNotCompleted()
    {
        Domain.Entities.Application application = BuildApplication(ApplicationStatus.Interview);
        application.Interviews.Add(new Interview { Id = Guid.NewGuid(), ApplicationId = application.Id, Status = InterviewStatus.Scheduled });
        OfferService service = BuildOfferService(application, out _);

        var response = await service.SendOfferAsync(application.Id.ToString(), WorkflowActorId, new[] { "HR" }, BuildOfferRequest());

        response.StatusCode.Should().Be(422);
        response.ErrorCode.Should().Be(ErrorCodes.InterviewNotCompleted);
        application.Status.Should().Be(ApplicationStatus.Interview);
    }

    // ---- T-WF-007: Interview -> Rejected blocked when interview scheduled but not completed ----
    [Fact]
    public async Task InterviewToReject_Blocked_WhenInterviewNotCompleted()
    {
        Domain.Entities.Application application = BuildApplication(ApplicationStatus.Interview);
        application.Interviews.Add(new Interview { Id = Guid.NewGuid(), ApplicationId = application.Id, Status = InterviewStatus.Scheduled });
        ApplicationService service = BuildApplicationService(application);

        var response = await service.SendRejectionEmailAsync(
            application.Id.ToString(), Guid.NewGuid(), BuildRejectionRequest());

        response.StatusCode.Should().Be(422);
        response.ErrorCode.Should().Be(ErrorCodes.InterviewNotCompleted);
        application.Status.Should().Be(ApplicationStatus.Interview);
    }

    // ---- T-WF-008: a direct decision to Offer is rejected — must use the offer email flow ----
    [Fact]
    public async Task InterviewToOffer_RequiresOfferEmail()
    {
        Domain.Entities.Application application = BuildApplication(ApplicationStatus.Interview);
        application.Interviews.Add(new Interview { Id = Guid.NewGuid(), ApplicationId = application.Id, Status = InterviewStatus.Completed });
        ApplicationService service = BuildApplicationService(application);

        var response = await service.UpdateApplicationDecisionAsync(
            application.Id.ToString(), Guid.NewGuid(), new UpdateApplicationDecisionRequest { TargetStatus = "Offer" });

        response.StatusCode.Should().Be(422);
        response.ErrorCode.Should().Be(ErrorCodes.EmailRequiredForOffer);
        application.Status.Should().Be(ApplicationStatus.Interview);
    }

    // ---- T-WF-009: a direct decision to Rejected is rejected — must use the rejection email flow ----
    [Fact]
    public async Task InterviewToRejected_RequiresRejectionEmail()
    {
        Domain.Entities.Application application = BuildApplication(ApplicationStatus.Interview);
        application.Interviews.Add(new Interview { Id = Guid.NewGuid(), ApplicationId = application.Id, Status = InterviewStatus.Completed });
        ApplicationService service = BuildApplicationService(application);

        var response = await service.UpdateApplicationDecisionAsync(
            application.Id.ToString(), Guid.NewGuid(), new UpdateApplicationDecisionRequest { TargetStatus = "Rejected" });

        response.StatusCode.Should().Be(422);
        response.ErrorCode.Should().Be(ErrorCodes.EmailRequiredForRejection);
        application.Status.Should().Be(ApplicationStatus.Interview);
    }

    // ---- T-WF-010: sending the offer email after a completed interview transitions to Offer ----
    [Fact]
    public async Task SendOfferEmail_AfterCompletedInterview_TransitionsToOffer()
    {
        Domain.Entities.Application application = BuildApplication(ApplicationStatus.Interview);
        application.Interviews.Add(new Interview { Id = Guid.NewGuid(), ApplicationId = application.Id, Status = InterviewStatus.Completed });
        OfferService service = BuildOfferService(application, out Mock<IEmailService> emailService);

        var response = await service.SendOfferAsync(application.Id.ToString(), WorkflowActorId, new[] { "HR" }, BuildOfferRequest());

        response.StatusCode.Should().Be(200);
        application.Status.Should().Be(ApplicationStatus.Offer);
        emailService.Verify(email => email.SendOfferEmailAsync(
            application.User.Email, application.User.FullName, application.Job.Title, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    // ---- T-WF-011: sending the rejection email after a completed interview transitions to Rejected ----
    [Fact]
    public async Task SendRejectionEmail_AfterCompletedInterview_TransitionsToRejected()
    {
        Domain.Entities.Application application = BuildApplication(ApplicationStatus.Interview);
        application.Interviews.Add(new Interview { Id = Guid.NewGuid(), ApplicationId = application.Id, Status = InterviewStatus.Completed });
        var emailService = new Mock<IEmailService>();
        Guid reviewerId = Guid.NewGuid();
        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(repository => repository.GetByIdAsync(reviewerId))
            .ReturnsAsync(new User { Id = reviewerId, Email = "actor@test.com", FullName = "HR Actor", Username = "hr.actor" });
        ApplicationService service = BuildApplicationService(application, emailService: emailService, userRepository: userRepository);

        var response = await service.SendRejectionEmailAsync(
            application.Id.ToString(), reviewerId, BuildRejectionRequest());

        response.StatusCode.Should().Be(200);
        application.Status.Should().Be(ApplicationStatus.Rejected);
        emailService.Verify(email => email.SendRejectionEmailAsync(
            application.User.Email, application.User.FullName, application.Job.Title, It.IsAny<string>(), It.IsAny<string>(), "actor@test.com"), Times.Once);
    }

    // ---- T-WF-012: an email send failure must NOT transition the application status ----
    [Fact]
    public async Task EmailFailure_DoesNotTransitionStatus()
    {
        Domain.Entities.Application application = BuildApplication(ApplicationStatus.Interview);
        application.Interviews.Add(new Interview { Id = Guid.NewGuid(), ApplicationId = application.Id, Status = InterviewStatus.Completed });

        var emailService = new Mock<IEmailService>();
        emailService
            .Setup(email => email.SendRejectionEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("SMTP down"));

        ApplicationService service = BuildApplicationService(application, emailService: emailService);

        var response = await service.SendRejectionEmailAsync(
            application.Id.ToString(), Guid.NewGuid(), BuildRejectionRequest());

        response.StatusCode.Should().Be(422);
        response.ErrorCode.Should().Be(ErrorCodes.EmailSendFailed);
        application.Status.Should().Be(ApplicationStatus.Interview);
    }

    // ---- T-WF-013: a direct status update to Offer OR Rejected returns 422 (both targets) ----
    [Theory]
    [InlineData("Offer", ErrorCodes.EmailRequiredForOffer)]
    [InlineData("Rejected", ErrorCodes.EmailRequiredForRejection)]
    public async Task DirectStatusUpdate_ToOfferOrRejected_Returns422(string targetStatus, string expectedCode)
    {
        Domain.Entities.Application application = BuildApplication(ApplicationStatus.Interview);
        application.Interviews.Add(new Interview { Id = Guid.NewGuid(), ApplicationId = application.Id, Status = InterviewStatus.Completed });
        ApplicationService service = BuildApplicationService(application);

        var response = await service.UpdateApplicationDecisionAsync(
            application.Id.ToString(), Guid.NewGuid(), new UpdateApplicationDecisionRequest { TargetStatus = targetStatus });

        response.StatusCode.Should().Be(422);
        response.ErrorCode.Should().Be(expectedCode);
        application.Status.Should().Be(ApplicationStatus.Interview);
    }

    // ---- T-WF-014: scheduling an interview is allowed while the application is in the Interview stage ----
    [Fact]
    public async Task ScheduleInterview_Allowed_WhenApplicationInInterview()
    {
        Domain.Entities.Application application = BuildApplication(ApplicationStatus.Interview);

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(repository => repository.GetTrackedByIdAsync(application.Id)).ReturnsAsync(application);

        var service = new InterviewService(
            Mock.Of<IInterviewRepository>(),
            applicationRepository.Object,
            Mock.Of<IUserRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<INotificationEventService>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<InterviewService>.Instance,
            Mock.Of<AutoMapper.IMapper>());

        var response = await service.CreateInterviewAsync(new CreateInterviewRequest
        {
            ApplicationId = application.Id.ToString(),
            Date = new DateOnly(2026, 7, 1),
            StartMinutes = 540,
            DurationMinutes = 60,
            Mode = "video",
            LocationOrLink = "https://meet.example/abc",
            Status = "scheduled"
        });

        response.StatusCode.Should().Be(201);
    }

    // ---- T-WF-015: the review detail DTO exposes the status + interview data the FE schedule button needs ----
    [Fact]
    public async Task ScheduleInterview_ButtonData_AvailableInReviewDto()
    {
        Domain.Entities.Application application = BuildApplication(ApplicationStatus.Interview);
        application.Interviews.Add(new Interview
        {
            Id = Guid.NewGuid(),
            ApplicationId = application.Id,
            InterviewDate = new DateTime(2026, 7, 1, 9, 0, 0),
            Status = InterviewStatus.Scheduled
        });
        ApplicationService service = BuildApplicationService(application);

        // Read-detail is ownership-scoped; the owning recruiter (WorkflowActorId == AssignedRecruiterId) can see it.
        var response = await service.GetApplicationReviewDetailAsync(
            application.Id.ToString(), WorkflowActorId, new[] { "HR" });

        response.StatusCode.Should().Be(200);
        response.Data!.Status.Should().Be("Interview");
        response.Data.Interviews.Should().ContainSingle();
        response.Data.Interviews[0].Status.Should().Be("Scheduled");
    }

    // ---- helpers ----

    private static UpsertApplicationOfferRequest BuildOfferRequest() => new()
    {
        BaseSalary = 2500,
        CurrencyCode = "USD",
        EmploymentType = "Full-time"
    };

    private static SendRejectionEmailRequest BuildRejectionRequest() => new()
    {
        Subject = "Application update",
        Body = "Thank you for your time; we will not be moving forward."
    };

    private static Domain.Entities.Application BuildApplication(ApplicationStatus status, Guid? assignedHeadId = null)
    {
        Guid applicationId = Guid.NewGuid();
        Guid candidateId = Guid.NewGuid();
        Guid jobId = Guid.NewGuid();

        return new Domain.Entities.Application
        {
            Id = applicationId,
            UserId = candidateId,
            JobId = jobId,
            Status = status,
            AssignedRecruiterId = WorkflowActorId,
            AssignedDepartmentHeadId = assignedHeadId,
            AppliedAt = new DateTime(2026, 6, 1),
            User = new User { Id = candidateId, Username = "candidate", FullName = "Candidate", Email = "c@test.com" },
            Job = new Job
            {
                Id = jobId,
                Title = "Backend Engineer",
                Location = "HCMC",
                Description = "Build the platform.",
                Status = JobStatus.Approved,
                EmploymentType = EmploymentType.FullTime
            }
        };
    }

    private static ApplicationService BuildApplicationService(
        Domain.Entities.Application application,
        Mock<IApplicationRepository>? applicationRepository = null,
        Mock<IEmailService>? emailService = null,
        Mock<IUserRepository>? userRepository = null)
    {
        applicationRepository ??= new Mock<IApplicationRepository>();
        applicationRepository.Setup(repository => repository.GetTrackedByIdAsync(application.Id)).ReturnsAsync(application);
        applicationRepository.Setup(repository => repository.GetByIdAsync(application.Id)).ReturnsAsync(application);

        return new ApplicationService(
            applicationRepository.Object,
            Mock.Of<ICandidateProfileRepository>(),
            (userRepository ?? new Mock<IUserRepository>()).Object,
            Mock.Of<IJobRepository>(),
            Mock.Of<IOfferRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IFileStorageService>(),
            Mock.Of<IApplicationSemanticProcessingQueue>(),
            Mock.Of<INotificationEventService>(),
            (emailService ?? new Mock<IEmailService>()).Object,
            Mock.Of<ILogger<ApplicationService>>());
    }

    private static OfferService BuildOfferService(Domain.Entities.Application application, out Mock<IEmailService> emailService)
    {
        emailService = new Mock<IEmailService>();

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(repository => repository.GetTrackedByIdAsync(application.Id)).ReturnsAsync(application);
        applicationRepository.Setup(repository => repository.GetByIdAsync(application.Id)).ReturnsAsync(application);

        var offerRepository = new Mock<IOfferRepository>();
        offerRepository.Setup(repository => repository.GetTrackedByApplicationIdAsync(application.Id)).ReturnsAsync((ApplicationOffer?)null);
        offerRepository.Setup(repository => repository.GetByApplicationIdAsync(application.Id))
            .ReturnsAsync(() => application.Offer);
        offerRepository.Setup(repository => repository.GetCurrenciesAsync())
            .ReturnsAsync([new OfferCurrency { Code = "USD", Name = "US Dollar", Symbol = "$" }]);
        offerRepository.Setup(repository => repository.GetBenefitsAsync()).ReturnsAsync([]);
        offerRepository.Setup(repository => repository.GetTemplatesAsync()).ReturnsAsync([]);
        offerRepository.Setup(repository => repository.AddAsync(It.IsAny<ApplicationOffer>()))
            .Callback<ApplicationOffer>(offer => application.Offer = offer)
            .Returns(Task.CompletedTask);

        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(repository => repository.GetUsersInRolesAsync(It.IsAny<string[]>())).ReturnsAsync([]);

        return new OfferService(
            applicationRepository.Object,
            offerRepository.Object,
            userRepository.Object,
            Mock.Of<IUnitOfWork>(),
            emailService.Object,
            Mock.Of<INotificationEventService>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<OfferService>.Instance);
    }
}
