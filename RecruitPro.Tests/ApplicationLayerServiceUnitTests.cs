using AutoMapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecruitPro.Application.Configurations;
using RecruitPro.Application.DTOs.Request.Applications;
using RecruitPro.Application.DTOs.Request.Auth;
using RecruitPro.Application.DTOs.Request.Candidate;
using RecruitPro.Application.DTOs.Request.Copilot;
using RecruitPro.Application.DTOs.Request.Discovery;
using RecruitPro.Application.DTOs.Request.Interviews;
using RecruitPro.Application.DTOs.Request.Jobs;
using RecruitPro.Application.DTOs.Request.Offers;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.DTOs.Response.Copilot;
using RecruitPro.Application.Exceptions;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Application.Mappings;
using RecruitPro.Application.Services;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;

namespace RecruitPro.Tests;

public sealed class ApplicationSemanticScoringServiceUnitTests
{
    [Fact]
    public async Task ProcessAsync_WhenApplicationMissing_CompletesWithoutThrowing()
    {
        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(repository => repository.GetTrackedByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Domain.Entities.Application?)null);

        var service = new ApplicationSemanticScoringService(
            applicationRepository.Object,
            Mock.Of<IEmbeddingProvider>(),
            Mock.Of<IEmbeddingCache>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<ILogger<ApplicationSemanticScoringService>>());

        Func<Task> act = () => service.ProcessAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ProcessAsync_WhenCandidateProfileMissing_MarksSemanticFailure()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var applicationRepository = new Mock<IApplicationRepository>();
        var application = new Domain.Entities.Application
        {
            Id = Guid.NewGuid(),
            RuleScore = 75,
            User = new User(),
            Job = new Job()
        };

        applicationRepository.Setup(repository => repository.GetTrackedByIdAsync(application.Id))
            .ReturnsAsync(application);

        var service = new ApplicationSemanticScoringService(
            applicationRepository.Object,
            Mock.Of<IEmbeddingProvider>(),
            Mock.Of<IEmbeddingCache>(),
            unitOfWork.Object,
            Mock.Of<ILogger<ApplicationSemanticScoringService>>());

        await service.ProcessAsync(application.Id);

        application.ScoreStatus.Should().Be("SemanticFailed");
        application.ScoreError.Should().Be("Candidate profile is missing.");
        applicationRepository.Verify(repository => repository.UpdateAsync(application), Times.Once);
        unitOfWork.Verify(workflow => workflow.BeginTransactionAsync(), Times.Once);
        unitOfWork.Verify(workflow => workflow.SaveChangesAsync(), Times.Once);
        unitOfWork.Verify(workflow => workflow.CommitAsync(), Times.Once);
    }
}

public sealed class ApplicationServiceUnitTests
{
    [Fact]
    public async Task ApplyAsync_WhenCandidateAlreadyApplied_ReturnsBadRequest()
    {
        var jobRepository = new Mock<IJobRepository>();
        var candidateRepository = new Mock<ICandidateProfileRepository>();
        var applicationRepository = new Mock<IApplicationRepository>();

        Guid userId = Guid.NewGuid();
        Guid jobId = Guid.NewGuid();
        var job = new Job
        {
            Id = jobId,
            Title = "Backend Engineer",
            Location = "HCMC",
            Description = "desc",
            Status = JobStatus.Approved,
            WorkMode = WorkMode.Remote,
            EmploymentType = EmploymentType.FullTime
        };
        var profile = new CandidateProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            User = new User { Id = userId, Username = "candidate.user", FullName = "Candidate", Email = "candidate@test.com" }
        };

        jobRepository.Setup(repository => repository.GetByIdAsync(jobId)).ReturnsAsync(job);
        candidateRepository.Setup(repository => repository.GetByUserIdAsync(userId)).ReturnsAsync(profile);
        applicationRepository.Setup(repository => repository.GetByUserIdAsync(userId))
            .ReturnsAsync(
            [
                new Domain.Entities.Application
                {
                    Id = Guid.NewGuid(),
                    JobId = jobId,
                    Status = ApplicationStatus.Applied
                }
            ]);

        var service = new ApplicationService(
            applicationRepository.Object,
            candidateRepository.Object,
            jobRepository.Object,
            Mock.Of<IOfferRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IFileStorageService>(),
            Mock.Of<IApplicationSemanticProcessingQueue>(),
            Mock.Of<ILogger<ApplicationService>>());

        var response = await service.ApplyAsync(userId, jobId.ToString(), new ApplyJobRequest());

        response.Success.Should().BeFalse();
        response.StatusCode.Should().Be(400);
        response.Message.Should().Be("Candidate already applied for this job.");
    }
}

public sealed class AuthServiceUnitTests
{
    [Fact]
    public async Task CandidateLoginAsync_WhenCredentialsValid_ReturnsMappedTokens()
    {
        var userRepository = new Mock<IUserRepository>();
        var jwtService = new Mock<IJwtService>();
        IMapper mapper = TestMapperFactory.Create();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "candidate.user",
            Email = "candidate@test.com",
            FullName = "Candidate User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pass@123"),
            UserRoles =
            [
                new UserRole
                {
                    Role = new Role { Name = "Candidate" }
                }
            ]
        };

        userRepository.Setup(repository => repository.GetByUsernameAsync(user.Username)).ReturnsAsync(user);
        jwtService.Setup(service => service.GenerateToken(user, "Access")).Returns("access-token");
        jwtService.Setup(service => service.GenerateToken(user, "Refresh")).Returns("refresh-token");

        var service = new AuthService(
            userRepository.Object,
            jwtService.Object,
            Mock.Of<IEmailService>(),
            Mock.Of<IUnitOfWork>(),
            mapper);

        var response = await service.CandidateLoginAsync(user.Username, "Pass@123");

        response.Success.Should().BeTrue();
        response.Data!.AccessToken.Should().Be("access-token");
        response.Data.RefreshToken.Should().Be("refresh-token");
        response.Data.User.Roles.Should().Contain("Candidate");
    }

    [Fact]
    public async Task ForgotCandidatePasswordAsync_WhenIdentifierBlank_ReturnsBadRequest()
    {
        var service = new AuthService(
            Mock.Of<IUserRepository>(),
            Mock.Of<IJwtService>(),
            Mock.Of<IEmailService>(),
            Mock.Of<IUnitOfWork>(),
            TestMapperFactory.Create());

        var response = await service.ForgotCandidatePasswordAsync("   ");

        response.StatusCode.Should().Be(400);
        response.Message.Should().Be("Identifier is required.");
    }
}

public sealed class CandidateServiceUnitTests
{
    [Fact]
    public async Task GenerateImportTemplateAsync_ReturnsExcelTemplate()
    {
        var service = CreateCandidateService();

        CandidateImportTemplateDto result = await service.GenerateImportTemplateAsync();

        result.FileName.Should().Be("candidate-import-template.xlsx");
        result.ContentType.Should().Contain("spreadsheetml");
        result.Content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetResumeDownloadUrlAsync_WhenResumeMissing_ReturnsNotFound()
    {
        var repository = new Mock<ICandidateProfileRepository>();
        repository.Setup(value => value.GetByResumeIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((CandidateProfile?)null);

        var service = CreateCandidateService(candidateRepository: repository.Object);

        var response = await service.GetResumeDownloadUrlAsync(Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(404);
        response.Message.Should().Be("Resume not found.");
    }

    [Fact]
    public async Task ParseResumeAsync_WhenAiOmitsPhone_DoesNotFallbackToExistingProfilePhone()
    {
        Guid userId = Guid.NewGuid();
        var profile = new CandidateProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            User = new User
            {
                Id = userId,
                FullName = "Old Name",
                Email = "old@example.com",
                Phone = "12123123"
            },
            CurrentPosition = "Old Headline",
            Address = "Old Address",
            Bio = "Old Bio",
            GithubUrl = "https://github.com/old",
            LinkedinUrl = "https://linkedin.com/in/old"
        };

        var repository = new Mock<ICandidateProfileRepository>();
        repository.Setup(value => value.GetByUserIdAsync(userId)).ReturnsAsync(profile);

        var skillRepository = new Mock<ISkillRepository>();
        skillRepository.Setup(value => value.GetAllAsync()).ReturnsAsync([]);

        var extractor = new Mock<IResumeTextExtractor>();
        extractor.Setup(value => value.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("John Doe\nBackend Developer\njohn@example.com\nThis is a sample resume content long enough to parse.");

        var aiProvider = new Mock<IResumeParsingAiProvider>();
        aiProvider.Setup(value => value.TryParseResumeAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<Skill>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResumeParsingAiResult
            {
                UsedAi = true,
                ModelName = "test-model",
                Data = new CandidateResumeAiParseDto
                {
                    Profile = new CandidateResumeAiProfileDto
                    {
                        Name = "John Doe",
                        Headline = "Backend Developer",
                        Email = "john@example.com",
                        Phone = null
                    }
                }
            });

        var service = CreateCandidateService(
            candidateRepository: repository.Object,
            skillRepository: skillRepository.Object,
            resumeTextExtractor: extractor.Object,
            resumeParsingAiProvider: aiProvider.Object);

        await using MemoryStream stream = new("dummy pdf content"u8.ToArray());
        var response = await service.ParseResumeAsync(userId, stream, "resume.pdf", "application/pdf");

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Profile.Phone.Should().BeEmpty();
        response.Data.Profile.Email.Should().Be("john@example.com");
    }

    [Fact]
    public async Task ParseResumeAsync_WhenHeuristicOmitsPhone_DoesNotFallbackToExistingProfilePhone()
    {
        Guid userId = Guid.NewGuid();
        var profile = new CandidateProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            User = new User
            {
                Id = userId,
                FullName = "Old Name",
                Email = "old@example.com",
                Phone = "12123123"
            },
            CurrentPosition = "Old Headline",
            Address = "Old Address",
            Bio = "Old Bio",
            GithubUrl = "https://github.com/old",
            LinkedinUrl = "https://linkedin.com/in/old"
        };

        var repository = new Mock<ICandidateProfileRepository>();
        repository.Setup(value => value.GetByUserIdAsync(userId)).ReturnsAsync(profile);

        var skillRepository = new Mock<ISkillRepository>();
        skillRepository.Setup(value => value.GetAllAsync()).ReturnsAsync([]);

        var extractor = new Mock<IResumeTextExtractor>();
        extractor.Setup(value => value.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("John Doe\nBackend Developer\njohn@example.com\nSummary section with enough content to pass parsing threshold and still no phone number.");

        var aiProvider = new Mock<IResumeParsingAiProvider>();
        aiProvider.Setup(value => value.TryParseResumeAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<Skill>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResumeParsingAiResult
            {
                UsedAi = false,
                FailureReason = "AI unavailable"
            });

        var service = CreateCandidateService(
            candidateRepository: repository.Object,
            skillRepository: skillRepository.Object,
            resumeTextExtractor: extractor.Object,
            resumeParsingAiProvider: aiProvider.Object);

        await using MemoryStream stream = new("dummy pdf content"u8.ToArray());
        var response = await service.ParseResumeAsync(userId, stream, "resume.pdf", "application/pdf");

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Profile.Phone.Should().BeEmpty();
        response.Data.Profile.Email.Should().Be("john@example.com");
    }

    private static CandidateService CreateCandidateService(
        ICandidateProfileRepository? candidateRepository = null,
        IUserRepository? userRepository = null,
        ISkillRepository? skillRepository = null,
        IUnitOfWork? unitOfWork = null,
        IFileStorageService? fileStorageService = null,
        IEmailService? emailService = null,
        IResumeTextExtractor? resumeTextExtractor = null,
        IResumeParsingAiProvider? resumeParsingAiProvider = null)
    {
        return new CandidateService(
            candidateRepository ?? Mock.Of<ICandidateProfileRepository>(),
            userRepository ?? Mock.Of<IUserRepository>(),
            skillRepository ?? Mock.Of<ISkillRepository>(),
            unitOfWork ?? Mock.Of<IUnitOfWork>(),
            fileStorageService ?? Mock.Of<IFileStorageService>(),
            emailService ?? Mock.Of<IEmailService>(),
            resumeTextExtractor ?? Mock.Of<IResumeTextExtractor>(),
            resumeParsingAiProvider ?? Mock.Of<IResumeParsingAiProvider>(),
            Mock.Of<ISemanticDiscoveryService>(),
            TestMapperFactory.Create(),
            Mock.Of<ILogger<CandidateService>>());
    }
}

public sealed class CopilotServiceUnitTests
{
    [Fact]
    public async Task GetJobsAsync_ReturnsRepositoryJobOptions()
    {
        var repository = new Mock<ICopilotRepository>();
        repository.Setup(value => value.GetJobOptionsAsync())
            .ReturnsAsync([new CopilotJobOptionDto { JobId = Guid.NewGuid(), Title = "Senior .NET" }]);

        var service = new CopilotService(
            repository.Object,
            Mock.Of<IFileStorageService>(),
            Mock.Of<IResumeTextExtractor>(),
            Mock.Of<IAiCopilotProvider>(),
            Mock.Of<IUnitOfWork>(),
            Options.Create(new AiProviderSettings()),
            TestMapperFactory.Create());

        var response = await service.GetJobsAsync();

        response.Success.Should().BeTrue();
        response.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateConversationAsync_WhenExistingConversationFound_ReturnsExistingConversation()
    {
        var repository = new Mock<ICopilotRepository>();
        Guid jobId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        var existingConversation = new CopilotConversation { Id = Guid.NewGuid(), JobId = jobId, UserId = userId, Title = "Existing" };

        repository.Setup(value => value.GetLatestConversationAsync(jobId, userId)).ReturnsAsync(existingConversation);

        var service = new CopilotService(
            repository.Object,
            Mock.Of<IFileStorageService>(),
            Mock.Of<IResumeTextExtractor>(),
            Mock.Of<IAiCopilotProvider>(),
            Mock.Of<IUnitOfWork>(),
            Options.Create(new AiProviderSettings()),
            TestMapperFactory.Create());

        var response = await service.CreateConversationAsync(new CreateCopilotConversationRequest { JobId = jobId }, userId);

        response.Success.Should().BeTrue();
        response.Data!.ConversationId.Should().Be(existingConversation.Id);
    }
}

public sealed class DashboardServiceUnitTests
{
    [Fact]
    public async Task GetHrDashboardAsync_ComposesDashboardFromRepositories()
    {
        var applicationRepository = new Mock<IApplicationRepository>();
        var interviewRepository = new Mock<IInterviewRepository>();
        var jobRepository = new Mock<IJobRepository>();

        applicationRepository.Setup(value => value.CountAsync()).ReturnsAsync(5);
        applicationRepository.Setup(value => value.GetRecentAsync(5)).ReturnsAsync(
        [
            new Domain.Entities.Application
            {
                Id = Guid.NewGuid(),
                AppliedAt = new DateTime(2026, 1, 1),
                Status = ApplicationStatus.Applied,
                User = new User { Username = "candidate.user", FullName = "Candidate", Email = "candidate@test.com" },
                Job = new Job { Title = "Backend" }
            }
        ]);
        interviewRepository.Setup(value => value.CountOnDateAsync(It.IsAny<DateTime>())).ReturnsAsync(2);
        interviewRepository.Setup(value => value.GetNextAsync(It.IsAny<DateTime>())).ReturnsAsync(new Interview { Id = Guid.NewGuid(), InterviewDate = new DateTime(2026, 1, 1, 9, 0, 0) });
        jobRepository.Setup(value => value.CountApprovedJobsAsync()).ReturnsAsync(3);
        jobRepository.Setup(value => value.GetPendingApprovalJobsAsync(5)).ReturnsAsync([new Job { Id = Guid.NewGuid(), Title = "Pending", WorkMode = WorkMode.Remote, Department = new Department { Name = "Engineering" } }]);

        var service = new DashboardService(
            Mock.Of<ICandidateProfileRepository>(),
            applicationRepository.Object,
            jobRepository.Object,
            interviewRepository.Object,
            Mock.Of<INotificationRepository>(),
            Mock.Of<ISemanticDiscoveryService>());

        var response = await service.GetHrDashboardAsync();

        response.Success.Should().BeTrue();
        response.Data!.Stats.ActivePostings.Should().Be(3);
        response.Data.Stats.TotalApplicants.Should().Be(5);
        response.Data.RecentApplications.Should().ContainSingle();
    }
}

public sealed class InterviewServiceUnitTests
{
    [Fact]
    public async Task GetScheduleDataAsync_WhenApplicationIdInvalid_ReturnsBadRequest()
    {
        var service = new InterviewService(
            Mock.Of<IInterviewRepository>(),
            Mock.Of<IApplicationRepository>(),
            Mock.Of<IUserRepository>(),
            Mock.Of<IUnitOfWork>(),
            TestMapperFactory.Create());

        var response = await service.GetScheduleDataAsync("not-a-guid");

        response.StatusCode.Should().Be(400);
        response.Message.Should().Be("Invalid application id.");
    }

    [Fact]
    public async Task UpdateInterviewStatusAsync_WhenStatusInvalid_ReturnsBadRequest()
    {
        Guid interviewId = Guid.NewGuid();
        var repository = new Mock<IInterviewRepository>();
        repository.Setup(value => value.GetTrackedByIdAsync(interviewId)).ReturnsAsync(new Interview { Id = interviewId });

        var service = new InterviewService(
            repository.Object,
            Mock.Of<IApplicationRepository>(),
            Mock.Of<IUserRepository>(),
            Mock.Of<IUnitOfWork>(),
            TestMapperFactory.Create());

        var response = await service.UpdateInterviewStatusAsync(interviewId.ToString(), new UpdateInterviewStatusRequest { Status = "weird" });

        response.StatusCode.Should().Be(400);
        response.Message.Should().Be("Invalid interview status.");
    }
}

public sealed class JobServiceUnitTests
{
    [Fact]
    public async Task GetFiltersAsync_ReturnsStaticFiltersAndRepositorySkills()
    {
        var repository = new Mock<IJobRepository>();
        repository.Setup(value => value.GetAllSkillNamesAsync()).ReturnsAsync([".NET", "SQL"]);

        var service = new JobService(
            repository.Object,
            Mock.Of<IApplicationRepository>(),
            Mock.Of<ISkillRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<ISemanticDiscoveryService>(),
            TestMapperFactory.Create());

        var response = await service.GetFiltersAsync();

        response.Success.Should().BeTrue();
        response.Data!.Skills.Should().Contain([".NET", "SQL"]);
        response.Data.EmploymentTypes.Should().Contain("Full-time");
    }

    [Fact]
    public async Task DeleteJobAsync_WhenJobIdInvalid_ReturnsNotFound()
    {
        var service = new JobService(
            Mock.Of<IJobRepository>(),
            Mock.Of<IApplicationRepository>(),
            Mock.Of<ISkillRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<ISemanticDiscoveryService>(),
            TestMapperFactory.Create());

        var response = await service.DeleteJobAsync("bad-id");

        response.StatusCode.Should().Be(404);
        response.Message.Should().Be("Job not found.");
    }
}

public sealed class ManagerAnalyticsServiceUnitTests
{
    [Fact]
    public async Task GetRecruitmentAnalyticsAsync_BuildsOverviewAndTrend()
    {
        var applicationRepository = new Mock<IApplicationRepository>();
        var interviewRepository = new Mock<IInterviewRepository>();
        var jobRepository = new Mock<IJobRepository>();

        applicationRepository.Setup(value => value.GetStatusCountsAsync()).ReturnsAsync(new Dictionary<ApplicationStatus, int>
        {
            [ApplicationStatus.Applied] = 5,
            [ApplicationStatus.Screening] = 3,
            [ApplicationStatus.ManagerReview] = 2,
            [ApplicationStatus.Interview] = 1,
            [ApplicationStatus.Offer] = 1,
            [ApplicationStatus.Hired] = 1
        });
        applicationRepository.Setup(value => value.GetAverageReviewCycleDaysAsync()).ReturnsAsync(4.6);
        applicationRepository.Setup(value => value.CountActiveCandidatesAsync()).ReturnsAsync(7);
        applicationRepository.Setup(value => value.GetMonthlyApplicationVolumeAsync(It.IsAny<DateTime>(), 6))
            .ReturnsAsync(Enumerable.Range(0, 6).Select(index => (new DateTime(2026, 1, 1).AddMonths(index), index + 1)).ToList());
        applicationRepository.Setup(value => value.GetDepartmentPipelineSnapshotAsync())
            .ReturnsAsync([("Engineering", 4, 1, 1)]);
        applicationRepository.Setup(value => value.GetAverageReviewCycleByDepartmentAsync())
            .ReturnsAsync([("Engineering", 5)]);
        interviewRepository.Setup(value => value.CountUpcomingScheduledAsync(It.IsAny<DateTime>())).ReturnsAsync(2);
        interviewRepository.Setup(value => value.GetMonthlyCompletedVolumeAsync(It.IsAny<DateTime>(), 6))
            .ReturnsAsync(Enumerable.Range(0, 6).Select(index => (new DateTime(2026, 1, 1).AddMonths(index), index)).ToList());
        jobRepository.Setup(value => value.GetDepartmentOpenRoleSnapshotAsync())
            .ReturnsAsync([("Engineering", 2, "Recruiter A")]);

        var service = new ManagerAnalyticsService(
            applicationRepository.Object,
            interviewRepository.Object,
            jobRepository.Object);

        var response = await service.GetRecruitmentAnalyticsAsync();

        response.Success.Should().BeTrue();
        response.Data!.Overview.ActiveCandidates.Should().Be(7);
        response.Data.Trend.Labels.Should().HaveCount(6);
        response.Data.DepartmentBreakdown.Should().ContainSingle();
    }
}

public sealed class OfferServiceUnitTests
{
    [Fact]
    public async Task GetOfferEditorAsync_WhenApplicationIdInvalid_ReturnsNotFound()
    {
        var service = new OfferService(
            Mock.Of<IApplicationRepository>(),
            Mock.Of<IOfferRepository>(),
            Mock.Of<IUserRepository>(),
            Mock.Of<IUnitOfWork>());

        var response = await service.GetOfferEditorAsync("not-a-guid");

        response.StatusCode.Should().Be(404);
        response.Message.Should().Be("Application not found.");
    }

    [Fact]
    public async Task SaveDraftAsync_WhenCurrencyUnavailable_ReturnsBadRequest()
    {
        Guid applicationId = Guid.NewGuid();
        var applicationRepository = new Mock<IApplicationRepository>();
        var offerRepository = new Mock<IOfferRepository>();
        var application = new Domain.Entities.Application
        {
            Id = applicationId,
            Status = ApplicationStatus.Offer,
            User = new User { Username = "candidate.user", FullName = "Candidate", Email = "candidate@test.com" },
            Job = new Job { Title = "Backend", Department = new Department { Name = "Engineering" }, EmploymentType = EmploymentType.FullTime }
        };

        applicationRepository.Setup(value => value.GetTrackedByIdAsync(applicationId)).ReturnsAsync(application);
        offerRepository.Setup(value => value.GetCurrenciesAsync()).ReturnsAsync([]);

        var service = new OfferService(
            applicationRepository.Object,
            offerRepository.Object,
            Mock.Of<IUserRepository>(),
            Mock.Of<IUnitOfWork>());

        var response = await service.SaveDraftAsync(applicationId.ToString(), Guid.NewGuid(), new UpsertApplicationOfferRequest
        {
            CurrencyCode = "USD",
            EmploymentType = "Full-time"
        });

        response.StatusCode.Should().Be(400);
        response.Message.Should().Be("Selected currency is not available.");
    }
}

public sealed class SemanticDiscoveryServiceUnitTests
{
    [Fact]
    public async Task SearchTalentPoolAsync_WhenNoQueryOrJobId_ReturnsBadRequest()
    {
        var service = new SemanticDiscoveryService(
            Mock.Of<ICandidateProfileRepository>(),
            Mock.Of<IJobRepository>(),
            Mock.Of<IEmbeddingProvider>(),
            Mock.Of<IEmbeddingCache>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<ILogger<SemanticDiscoveryService>>());

        var response = await service.SearchTalentPoolAsync(new TalentPoolSearchRequest());

        response.StatusCode.Should().Be(400);
        response.Message.Should().Be("Please provide a search query or jobId.");
    }

    [Fact]
    public async Task GetRecommendedJobsForCandidateAsync_WhenProfileMissing_ThrowsNotFound()
    {
        var repository = new Mock<ICandidateProfileRepository>();
        repository.Setup(value => value.GetByUserIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((CandidateProfile?)null);

        var service = new SemanticDiscoveryService(
            repository.Object,
            Mock.Of<IJobRepository>(),
            Mock.Of<IEmbeddingProvider>(),
            Mock.Of<IEmbeddingCache>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<ILogger<SemanticDiscoveryService>>());

        Func<Task> act = () => service.GetRecommendedJobsForCandidateAsync(Guid.NewGuid(), 5);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}

internal static class TestMapperFactory
{
    private static readonly Lazy<IMapper> Mapper = new(() =>
    {
        MapperConfiguration configuration = new(config =>
        {
            config.AddProfile<UserProfile>();
            config.AddProfile<JobProfile>();
            config.AddProfile<SharedProfile>();
        });
        return configuration.CreateMapper();
    });

    public static IMapper Create() => Mapper.Value;
}
