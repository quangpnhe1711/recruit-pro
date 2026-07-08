using AutoMapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
using System.Text.Json;

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
            Mock.Of<INotificationEventService>(),
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
            Mock.Of<INotificationEventService>(),
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
    public async Task ApplyAsync_WhenCandidateAlreadyApplied_ReturnsConflict()
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
        // INV-003: duplicate detection is the EXISTS-active query.
        applicationRepository.Setup(repository => repository.HasActiveApplicationAsync(userId, jobId)).ReturnsAsync(true);

        var service = new ApplicationService(
            applicationRepository.Object,
            candidateRepository.Object,
            Mock.Of<IUserRepository>(),
            jobRepository.Object,
            Mock.Of<IOfferRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IFileStorageService>(),
            Mock.Of<IApplicationSemanticProcessingQueue>(),
            Mock.Of<INotificationEventService>(),
            Mock.Of<IEmailService>(),
            Mock.Of<ILogger<ApplicationService>>());

        var response = await service.ApplyAsync(userId, jobId.ToString(), new ApplyJobRequest());

        response.Success.Should().BeFalse();
        // Duplicate of an ACTIVE application is a conflict (409), not a generic bad request.
        response.StatusCode.Should().Be(409);
        response.ErrorCode.Should().Be(RecruitPro.Application.Common.ErrorCodes.ApplicationAlreadyActive);
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
            Mock.Of<ICandidateProfileRepository>(),
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
            Mock.Of<ICandidateProfileRepository>(),
            Mock.Of<IJwtService>(),
            Mock.Of<IEmailService>(),
            Mock.Of<IUnitOfWork>(),
            TestMapperFactory.Create());

        var response = await service.ForgotCandidatePasswordAsync("   ");

        response.StatusCode.Should().Be(400);
        response.ErrorCode.Should().Be(RecruitPro.Application.Common.ErrorCodes.Required);
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
        response.ErrorCode.Should().Be(RecruitPro.Application.Common.ErrorCodes.ResumeNotFound);
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

    // Phase 2.3 — upload hardening regression tests. Validation runs before any profile/storage access,
    // so a bad file is rejected with a stable errorCode and never reaches AI/extraction.
    [Fact]
    public async Task ParseResumeAsync_EmptyFile_ReturnsBadRequestWithErrorCode()
    {
        var service = CreateCandidateService();
        await using MemoryStream stream = new(Array.Empty<byte>());

        var response = await service.ParseResumeAsync(Guid.NewGuid(), stream, "resume.pdf", "application/pdf");

        response.Success.Should().BeFalse();
        response.StatusCode.Should().Be(400);
        response.ErrorCode.Should().Be(RecruitPro.Application.Common.ErrorCodes.ResumeFileEmpty);
    }

    [Fact]
    public async Task ParseResumeAsync_UnsupportedExtension_ReturnsBadRequestWithErrorCode()
    {
        var service = CreateCandidateService();
        await using MemoryStream stream = new("MZ malware"u8.ToArray());

        var response = await service.ParseResumeAsync(Guid.NewGuid(), stream, "malware.exe", "application/octet-stream");

        response.Success.Should().BeFalse();
        response.StatusCode.Should().Be(400);
        response.ErrorCode.Should().Be(RecruitPro.Application.Common.ErrorCodes.ResumeFileUnsupportedType);
    }

    [Fact]
    public async Task ParseResumeAsync_UnsupportedMimeForExtension_ReturnsBadRequestWithErrorCode()
    {
        var service = CreateCandidateService();
        await using MemoryStream stream = new("dummy pdf content"u8.ToArray());

        // A .pdf extension paired with an image content type is rejected as a mismatched MIME type.
        var response = await service.ParseResumeAsync(Guid.NewGuid(), stream, "resume.pdf", "image/png");

        response.Success.Should().BeFalse();
        response.StatusCode.Should().Be(400);
        response.ErrorCode.Should().Be(RecruitPro.Application.Common.ErrorCodes.ResumeFileUnsupportedType);
    }

    [Fact]
    public async Task ParseResumeAsync_OversizedFile_ReturnsBadRequestWithErrorCode()
    {
        var service = CreateCandidateService();
        // 6 MB > the 5 MB CV cap — rejected mid-stream before being fully buffered.
        await using MemoryStream stream = new(new byte[6 * 1024 * 1024]);

        var response = await service.ParseResumeAsync(Guid.NewGuid(), stream, "resume.pdf", "application/pdf");

        response.Success.Should().BeFalse();
        response.StatusCode.Should().Be(400);
        response.ErrorCode.Should().Be(RecruitPro.Application.Common.ErrorCodes.ResumeFileTooLarge);
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
        var callerUserId = Guid.NewGuid();
        var repository = new Mock<ICopilotRepository>();
        repository.Setup(value => value.GetJobOptionsAsync(callerUserId))
            .ReturnsAsync([new CopilotJobOptionDto { JobId = Guid.NewGuid(), Title = "Senior .NET" }]);

        var service = new CopilotService(
            repository.Object,
            Mock.Of<IJobRepository>(),
            Mock.Of<IApplicationRepository>(),
            Mock.Of<IApplicationService>(),
            Mock.Of<IFileStorageService>(),
            Mock.Of<IResumeTextExtractor>(),
            Mock.Of<IAiCopilotProvider>(),
            Mock.Of<IUnitOfWork>(),
            Options.Create(new AiProviderSettings()),
            TestMapperFactory.Create());

        var response = await service.GetJobsAsync(callerUserId);

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
            Mock.Of<IJobRepository>(),
            Mock.Of<IApplicationRepository>(),
            Mock.Of<IApplicationService>(),
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

    [Fact]
    public async Task SearchCandidatesAsync_IsDeprecated_NoProviderCallNoArtifact()
    {
        // v2 §1 — candidate search is soft-deprecated: deterministic screening-only view, no AI call,
        // no new artifact, and a clear deprecation warning.
        var repository = new Mock<ICopilotRepository>();
        var jobRepository = new Mock<IJobRepository>();
        var aiProvider = new Mock<IAiCopilotProvider>();
        Guid jobId = Guid.NewGuid();
        Guid ownerId = Guid.NewGuid();
        CopilotCandidatePoolDto pool = BuildCopilotPool(jobId);

        jobRepository.Setup(value => value.GetByIdAsync(jobId)).ReturnsAsync(new Job
        {
            Id = jobId,
            CreatedBy = ownerId,
            Title = "Senior .NET Engineer"
        });
        repository.Setup(value => value.GetCandidatePoolAsync(jobId)).ReturnsAsync(pool);

        CopilotService service = CreateService(repository.Object, jobRepository.Object, aiProvider: aiProvider.Object,
            aiSettings: new AiProviderSettings { Enabled = true, ApiKey = "test-key", Model = "test-model" });

        var response = await service.SearchCandidatesAsync(
            new NaturalLanguageCandidateSearchRequest
            {
                JobId = jobId,
                Query = "Find .NET candidates with SQL and at least 3 years",
                MaxResults = 2
            },
            ownerId,
            ["HR"]);

        response.Success.Should().BeTrue();
        response.Data!.Ai.FallbackUsed.Should().BeTrue();
        response.Data.Ai.ProviderName.Should().Be("deterministic-copilot");
        response.Data.Ai.Warnings.Should().Contain(warning => warning.StartsWith("candidate-search:deprecated", StringComparison.Ordinal));
        // No AI provider call and no new artifact for the deprecated flow.
        aiProvider.Verify(value => value.TryCreateStructuredJsonAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(value => value.AddGeneratedArtifactAsync(It.IsAny<CopilotGeneratedArtifact>()), Times.Never);
    }

    [Fact]
    public async Task AnalyzeCandidateFitAsync_DerivesFromLatestRanking_NoProviderCall_NoRerank()
    {
        // v2 §9 — fit analysis reads the latest ranking session's Vietnamese fit evaluation; it must
        // not call the provider and must not persist a new snapshot (that happens at ranking time).
        var repository = new Mock<ICopilotRepository>();
        var jobRepository = new Mock<IJobRepository>();
        var aiProvider = new Mock<IAiCopilotProvider>();
        Guid jobId = Guid.NewGuid();
        Guid ownerId = Guid.NewGuid();
        Guid candidateUserId = Guid.NewGuid();
        Guid applicationId = Guid.NewGuid();

        jobRepository.Setup(value => value.GetByIdAsync(jobId)).ReturnsAsync(new Job { Id = jobId, CreatedBy = ownerId, Title = "Senior .NET Engineer" });
        repository.Setup(value => value.GetCandidatePoolAsync(jobId)).ReturnsAsync(BuildCopilotPool(jobId));
        repository.Setup(value => value.GetLatestRankingSessionForJobAsync(jobId, ownerId)).ReturnsAsync(new CopilotRankingSession
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            UserId = ownerId,
            NormalizedRulesJson = "{}",
            Results =
            [
                new CopilotRankingResult
                {
                    CandidateUserId = candidateUserId,
                    ApplicationId = applicationId,
                    RankPosition = 1,
                    TotalScore = 90,
                    Recommendation = "Interview",
                    StrengthsJson = JsonSerializer.Serialize(new[] { ".NET" }),
                    WeaknessesJson = "[]",
                    ExplanationJson = JsonSerializer.Serialize(new { Summary = "Ứng viên phù hợp mạnh với vị trí.", IsAiGenerated = false, FitLabel = "StrongFit", ConfidenceScore = 90m, Evidence = new[] { "Điểm mạnh: .NET." } }),
                    Application = new Domain.Entities.Application { Id = applicationId, User = new User { FullName = "Strong Candidate" } }
                }
            ]
        });

        CopilotService service = CreateService(repository.Object, jobRepository.Object, aiProvider: aiProvider.Object,
            aiSettings: new AiProviderSettings { Enabled = true, ApiKey = "test-key", Model = "fit-model" });

        var response = await service.AnalyzeCandidateFitAsync(jobId, new CandidateFitAnalysisRequest(), ownerId, ["HR"]);

        response.Success.Should().BeTrue();
        response.Data!.Analyses.Should().ContainSingle();
        response.Data.Analyses[0].FitLabel.Should().Be("StrongFit");
        response.Data.Analyses[0].Summary.Should().Be("Ứng viên phù hợp mạnh với vị trí.");
        aiProvider.Verify(value => value.TryCreateStructuredJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(value => value.AddFitAnalysisAsync(It.IsAny<CandidateFitAnalysis>()), Times.Never);
    }

    [Fact]
    public async Task AnalyzeCandidateFitAsync_WhenNoRankingSession_ReturnsEmptyWithGuidance()
    {
        var repository = new Mock<ICopilotRepository>();
        var jobRepository = new Mock<IJobRepository>();
        Guid jobId = Guid.NewGuid();
        Guid ownerId = Guid.NewGuid();

        jobRepository.Setup(value => value.GetByIdAsync(jobId)).ReturnsAsync(new Job { Id = jobId, CreatedBy = ownerId, Title = "Senior .NET Engineer" });
        repository.Setup(value => value.GetCandidatePoolAsync(jobId)).ReturnsAsync(BuildCopilotPool(jobId));
        repository.Setup(value => value.GetLatestRankingSessionForJobAsync(jobId, ownerId)).ReturnsAsync((CopilotRankingSession?)null);

        CopilotService service = CreateService(repository.Object, jobRepository.Object);

        var response = await service.AnalyzeCandidateFitAsync(jobId, new CandidateFitAnalysisRequest(), ownerId, ["HR"]);

        response.Success.Should().BeTrue();
        response.Data!.Analyses.Should().BeEmpty();
        response.Data.Ai.Warnings.Should().Contain(warning => warning.Contains("xếp hạng"));
    }

    [Fact]
    public async Task GenerateInterviewQuestionsAsync_WhenProviderReturnsValidJson_PersistsProviderArtifact()
    {
        var repository = new Mock<ICopilotRepository>();
        var jobRepository = new Mock<IJobRepository>();
        var aiProvider = new Mock<IAiCopilotProvider>();
        Guid jobId = Guid.NewGuid();
        Guid ownerId = Guid.NewGuid();
        CopilotCandidatePoolDto pool = BuildCopilotPool(jobId);
        CopilotCandidateDto candidate = pool.Candidates[0];
        CopilotGeneratedArtifact? persistedArtifact = null;

        jobRepository.Setup(value => value.GetByIdAsync(jobId)).ReturnsAsync(new Job
        {
            Id = jobId,
            CreatedBy = ownerId,
            Title = "Senior .NET Engineer"
        });
        repository.Setup(value => value.GetCandidatePoolAsync(jobId)).ReturnsAsync(pool);
        repository.Setup(value => value.GetPromptTemplatesAsync(ownerId)).ReturnsAsync([]);
        repository.Setup(value => value.AddGeneratedArtifactAsync(It.IsAny<CopilotGeneratedArtifact>()))
            .Callback<CopilotGeneratedArtifact>(artifact => persistedArtifact = artifact)
            .Returns(Task.CompletedTask);

        InterviewQuestionSetDto providerResponse = new()
        {
            JobId = jobId,
            CandidateUserId = candidate.CandidateUserId,
            Focus = "backend depth",
            Questions =
            [
                new InterviewQuestionDto
                {
                    Category = "Technical",
                    Question = "How did you design the SQL-backed .NET APIs?",
                    Evidence = "Provider question evidence"
                }
            ]
        };

        aiProvider.Setup(value => value.TryCreateStructuredJsonAsync(
                "interview_questions",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiStructuredJsonResult.Success(JsonSerializer.Serialize(providerResponse), "question-provider", "question-model"));

        CopilotService service = CreateService(
            repository.Object,
            jobRepository.Object,
            aiProvider: aiProvider.Object,
            aiSettings: new AiProviderSettings { Enabled = true, ApiKey = "test-key", Model = "question-model" });

        var response = await service.GenerateInterviewQuestionsAsync(
            jobId,
            new InterviewQuestionRequest
            {
                CandidateUserId = candidate.CandidateUserId,
                Focus = "backend depth",
                QuestionCount = 3
            },
            ownerId,
            ["HR"]);

        response.Success.Should().BeTrue();
        response.Data!.Ai.FallbackUsed.Should().BeFalse();
        response.Data.Ai.ProviderName.Should().Be("question-provider");
        response.Data.Questions.Should().ContainSingle();
        response.Data.Questions[0].Evidence.Should().Be("Provider question evidence");
        persistedArtifact.Should().NotBeNull();
        persistedArtifact!.ArtifactType.Should().Be("interview_questions");
        persistedArtifact.ProviderName.Should().Be("question-provider");
        persistedArtifact.FallbackUsed.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateInterviewQuestionsAsync_WhenAlreadyGenerated_ReturnsCachedWithoutProvider()
    {
        // v2: interview questions are generated once per candidate; a repeat click returns the cached
        // artifact and never calls the provider or persists a new artifact.
        var repository = new Mock<ICopilotRepository>();
        var jobRepository = new Mock<IJobRepository>();
        var aiProvider = new Mock<IAiCopilotProvider>();
        Guid jobId = Guid.NewGuid();
        Guid ownerId = Guid.NewGuid();
        CopilotCandidatePoolDto pool = BuildCopilotPool(jobId);
        CopilotCandidateDto candidate = pool.Candidates[0];

        jobRepository.Setup(value => value.GetByIdAsync(jobId)).ReturnsAsync(new Job { Id = jobId, CreatedBy = ownerId, Title = "Senior .NET Engineer" });
        repository.Setup(value => value.GetCandidatePoolAsync(jobId)).ReturnsAsync(pool);

        var cachedSet = new InterviewQuestionSetDto
        {
            JobId = jobId,
            CandidateUserId = candidate.CandidateUserId,
            Focus = "job-fit",
            Questions = [new InterviewQuestionDto { Category = "Kỹ thuật", Question = "Câu hỏi đã lưu?", Evidence = "Bằng chứng" }],
            Ai = new CopilotAiMetadataDto { FallbackUsed = true, ProviderName = "deterministic-copilot", ModelName = "deterministic-copilot-v2" }
        };
        repository.Setup(value => value.GetGeneratedArtifactsAsync(ownerId, jobId, (Guid?)candidate.ApplicationId, "interview_questions", 1))
            .ReturnsAsync([new CopilotGeneratedArtifact
            {
                Id = Guid.NewGuid(),
                OwnerUserId = ownerId,
                JobId = jobId,
                ApplicationId = candidate.ApplicationId,
                ArtifactType = "interview_questions",
                PayloadJson = JsonSerializer.Serialize(cachedSet)
            }]);

        CopilotService service = CreateService(repository.Object, jobRepository.Object, aiProvider: aiProvider.Object,
            aiSettings: new AiProviderSettings { Enabled = true, ApiKey = "test-key", Model = "m" });

        var response = await service.GenerateInterviewQuestionsAsync(
            jobId,
            new InterviewQuestionRequest { CandidateUserId = candidate.CandidateUserId, ApplicationId = candidate.ApplicationId, Focus = "job-fit", QuestionCount = 5 },
            ownerId,
            ["HR"]);

        response.Success.Should().BeTrue();
        response.Data!.Questions.Should().ContainSingle();
        response.Data.Questions[0].Question.Should().Be("Câu hỏi đã lưu?");
        response.Data.Ai.Warnings.Should().Contain("interview-questions:cached");
        aiProvider.Verify(value => value.TryCreateStructuredJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(value => value.AddGeneratedArtifactAsync(It.IsAny<CopilotGeneratedArtifact>()), Times.Never);
    }

    [Fact]
    public async Task DraftApplicationEmailAsync_IsDeprecated_NoProviderCallNoArtifact()
    {
        // v2 §2 — AI email drafting is removed from the active flow: deterministic template only,
        // no provider call, no new artifact, clear deprecation warning.
        var repository = new Mock<ICopilotRepository>();
        var applicationRepository = new Mock<IApplicationRepository>();
        var aiProvider = new Mock<IAiCopilotProvider>();
        Guid ownerId = Guid.NewGuid();
        Guid applicationId = Guid.NewGuid();
        Guid jobId = Guid.NewGuid();

        applicationRepository.Setup(value => value.GetByIdAsync(applicationId)).ReturnsAsync(new Domain.Entities.Application
        {
            Id = applicationId,
            UserId = Guid.NewGuid(),
            JobId = jobId,
            AssignedRecruiterId = ownerId,
            Status = ApplicationStatus.Screening,
            User = new User { FullName = "Strong Candidate", Email = "candidate@test.com" },
            Job = new Job { Id = jobId, Title = "Senior .NET Engineer", CreatedBy = ownerId }
        });

        CopilotService service = CreateService(
            repository.Object,
            applicationRepository: applicationRepository.Object,
            aiProvider: aiProvider.Object,
            aiSettings: new AiProviderSettings { Enabled = true, ApiKey = "test-key", Model = "email-model" });

        var response = await service.DraftApplicationEmailAsync(
            applicationId,
            new HrEmailDraftRequest { TemplateType = "interview_invite" },
            ownerId,
            ["HR"]);

        response.Success.Should().BeTrue();
        response.Data!.Ai.FallbackUsed.Should().BeTrue();
        response.Data.Ai.Warnings.Should().Contain(warning => warning.StartsWith("email-draft:deprecated", StringComparison.Ordinal));
        response.Data.Subject.Should().NotBeNullOrWhiteSpace();
        aiProvider.Verify(value => value.TryCreateStructuredJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(value => value.AddGeneratedArtifactAsync(It.IsAny<CopilotGeneratedArtifact>()), Times.Never);
    }

    [Fact]
    public async Task DraftApplicationEmailAsync_WhenCallerOutOfScope_Returns403()
    {
        var applicationRepository = new Mock<IApplicationRepository>();
        Guid applicationId = Guid.NewGuid();
        applicationRepository.Setup(value => value.GetByIdAsync(applicationId)).ReturnsAsync(new Domain.Entities.Application
        {
            Id = applicationId,
            UserId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            AssignedRecruiterId = Guid.NewGuid(),
            Status = ApplicationStatus.Screening,
            User = new User { FullName = "Candidate User", Email = "candidate@test.com" },
            Job = new Job { Title = "Senior .NET Engineer", CreatedBy = Guid.NewGuid() }
        });

        CopilotService service = CreateService(applicationRepository: applicationRepository.Object);

        var response = await service.DraftApplicationEmailAsync(
            applicationId,
            new HrEmailDraftRequest { TemplateType = "interview_invite" },
            Guid.NewGuid(),
            ["HR"]);

        response.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task CreatePromptTemplateAsync_PersistsTemplateForCurrentUser()
    {
        var repository = new Mock<ICopilotRepository>();
        repository.Setup(value => value.AddPromptTemplateAsync(It.IsAny<CopilotPromptTemplate>()))
            .Returns(Task.CompletedTask);
        Guid userId = Guid.NewGuid();
        CopilotService service = CreateService(repository.Object);

        var response = await service.CreatePromptTemplateAsync(new CreateCopilotPromptTemplateRequest
        {
            Name = "Interview screen",
            TemplateType = "interview_questions",
            Prompt = "Focus on backend depth",
            IsActive = true
        }, userId);

        response.StatusCode.Should().Be(201);
        response.Data!.Name.Should().Be("Interview screen");
        repository.Verify(value => value.AddPromptTemplateAsync(It.Is<CopilotPromptTemplate>(template =>
            template.OwnerUserId == userId
            && template.TemplateType == "interview_questions"
            && template.Prompt == "Focus on backend depth")), Times.Once);
    }

    [Fact]
    public async Task GetLatestFitAnalysisForApplicationAsync_WhenInScope_ReturnsPersistedSnapshot()
    {
        var repository = new Mock<ICopilotRepository>();
        var applicationRepository = new Mock<IApplicationRepository>();
        Guid ownerId = Guid.NewGuid();
        Guid applicationId = Guid.NewGuid();
        Guid jobId = Guid.NewGuid();
        Guid candidateUserId = Guid.NewGuid();

        applicationRepository.Setup(value => value.GetByIdAsync(applicationId)).ReturnsAsync(new Domain.Entities.Application
        {
            Id = applicationId,
            UserId = candidateUserId,
            JobId = jobId,
            AssignedRecruiterId = ownerId,
            Job = new Job { Id = jobId, CreatedBy = ownerId }
        });
        repository.Setup(value => value.GetLatestFitAnalysisAsync(applicationId)).ReturnsAsync(new CandidateFitAnalysis
        {
            Id = Guid.NewGuid(),
            AuditId = Guid.NewGuid(),
            JobId = jobId,
            CandidateUserId = candidateUserId,
            ApplicationId = applicationId,
            FitLabel = "StrongFit",
            ConfidenceScore = 92,
            TotalScore = 88,
            StrengthsJson = JsonSerializer.Serialize(new[] { ".NET", "SQL" }),
            GapsJson = JsonSerializer.Serialize(Array.Empty<string>()),
            EvidenceJson = JsonSerializer.Serialize(new[] { "Scores: total 88" }),
            Summary = "Strong deterministic match",
            ProviderName = "deterministic-copilot",
            ModelName = "deterministic-copilot-v2",
            FallbackUsed = true,
            CandidateUser = new User { Id = candidateUserId, FullName = "Strong Candidate" }
        });

        CopilotService service = CreateService(repository.Object, applicationRepository: applicationRepository.Object);

        var response = await service.GetLatestFitAnalysisForApplicationAsync(applicationId, ownerId, ["HR"]);

        response.Success.Should().BeTrue();
        response.Data!.FitLabel.Should().Be("StrongFit");
        response.Data.FullName.Should().Be("Strong Candidate");
        response.Data.Strengths.Should().Contain(".NET");
    }

    [Fact]
    public async Task CreateRankingAsync_OnlyRanksScreeningApplications()
    {
        // v2 §6 — a candidate already at ManagerReview (Head Review) must be excluded from ranking.
        var repository = new Mock<ICopilotRepository>();
        Guid jobId = Guid.NewGuid();
        Guid ownerId = Guid.NewGuid();
        Guid conversationId = Guid.NewGuid();

        var pool = new CopilotCandidatePoolDto
        {
            Job = new CopilotJobContextDto { JobId = jobId, Title = "DevOps", RequiredSkills = ["Docker"] },
            Candidates =
            [
                new CopilotCandidateDto { CandidateUserId = Guid.NewGuid(), ApplicationId = Guid.NewGuid(), FullName = "Screening One", ExperienceYears = 4, Skills = ["Docker"], CvSummary = "Docker in production.", Status = "Screening" },
                new CopilotCandidateDto { CandidateUserId = Guid.NewGuid(), ApplicationId = Guid.NewGuid(), FullName = "Head Review One", ExperienceYears = 6, Skills = ["Docker"], CvSummary = "Docker expert.", Status = "ManagerReview" }
            ]
        };

        repository.Setup(v => v.GetConversationAsync(conversationId)).ReturnsAsync(new CopilotConversation { Id = conversationId, JobId = jobId, UserId = ownerId });
        repository.Setup(v => v.GetCandidatePoolAsync(jobId)).ReturnsAsync(pool);
        repository.Setup(v => v.GetSavedRulesAsync(jobId, ownerId)).ReturnsAsync([]);
        repository.Setup(v => v.GetLatestMatchingRankingSessionAsync(jobId, ownerId, It.IsAny<string>())).ReturnsAsync((CopilotRankingSession?)null);
        repository.Setup(v => v.GetNextMessageSequenceAsync(conversationId)).ReturnsAsync(1);

        CopilotService service = CreateService(repository.Object);

        var response = await service.CreateRankingAsync(conversationId, new CopilotPromptRequest
        {
            JobId = jobId,
            Prompt = "Xếp hạng ứng viên Docker",
            ForceRanking = true
        }, ownerId);

        response.Success.Should().BeTrue();
        response.Data!.DidRank.Should().BeTrue();
        response.Data.Results.Should().ContainSingle();
        response.Data.Results[0].FullName.Should().Be("Screening One");
        response.Data.Results[0].FitLabel.Should().NotBeNullOrWhiteSpace();
        response.Data.Results[0].Summary.Should().Contain("Ứng viên");
    }

    [Fact]
    public async Task CreateRankingAsync_ProviderCannotReorder_KeepsDeterministicOrder_EnrichesSummaries()
    {
        // v2 §13/hardening: provider may enrich Vietnamese prose but must not reorder by default, and
        // must not introduce unknown candidates.
        var repository = new Mock<ICopilotRepository>();
        var aiProvider = new Mock<IAiCopilotProvider>();
        Guid jobId = Guid.NewGuid();
        Guid ownerId = Guid.NewGuid();
        Guid conversationId = Guid.NewGuid();

        CopilotCandidatePoolDto pool = BuildCopilotPool(jobId);
        CopilotCandidateDto strong = pool.Candidates[0];
        CopilotCandidateDto junior = pool.Candidates[1];

        repository.Setup(v => v.GetConversationAsync(conversationId)).ReturnsAsync(new CopilotConversation { Id = conversationId, JobId = jobId, UserId = ownerId });
        repository.Setup(v => v.GetCandidatePoolAsync(jobId)).ReturnsAsync(pool);
        repository.Setup(v => v.GetSavedRulesAsync(jobId, ownerId)).ReturnsAsync([]);
        repository.Setup(v => v.GetLatestMatchingRankingSessionAsync(jobId, ownerId, It.IsAny<string>())).ReturnsAsync((CopilotRankingSession?)null);
        repository.Setup(v => v.GetNextMessageSequenceAsync(conversationId)).ReturnsAsync(1);

        // Provider returns REVERSED order + an unknown candidate + its own summaries.
        var providerResponse = new CopilotPromptResponseDto
        {
            ConversationId = conversationId,
            DidRank = true,
            NormalizedRules = new CopilotNormalizedRulesDto(),
            Results =
            [
                new CopilotRankingResultDto { CandidateUserId = junior.CandidateUserId, ApplicationId = junior.ApplicationId, FullName = "Junior", RankPosition = 1, TotalScore = 95, Recommendation = "Interview", Summary = "AI Junior tóm tắt" },
                new CopilotRankingResultDto { CandidateUserId = strong.CandidateUserId, ApplicationId = strong.ApplicationId, FullName = "Strong", RankPosition = 2, TotalScore = 40, Recommendation = "Hold", Summary = "AI Strong tóm tắt" },
                new CopilotRankingResultDto { CandidateUserId = Guid.NewGuid(), ApplicationId = Guid.NewGuid(), FullName = "Ghost", RankPosition = 3, TotalScore = 99, Recommendation = "Interview", Summary = "unknown" }
            ]
        };
        aiProvider.Setup(v => v.TryCreateRankingAsync(It.IsAny<CopilotCandidatePoolDto>(), It.IsAny<CopilotNormalizedRulesDto>(), It.IsAny<IReadOnlyList<CopilotRankingResultDto>>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(providerResponse);

        CopilotService service = CreateService(repository.Object, aiProvider: aiProvider.Object,
            aiSettings: new AiProviderSettings { Enabled = true, ApiKey = "test-key", Model = "m", AllowProviderReordering = false });

        var response = await service.CreateRankingAsync(conversationId, new CopilotPromptRequest
        {
            JobId = jobId,
            Prompt = "Xếp hạng ứng viên .NET SQL",
            ForceRanking = true
        }, ownerId);

        response.Success.Should().BeTrue();
        // Deterministic order preserved: Strong Candidate stays rank 1 despite provider reversing.
        response.Data!.Results[0].FullName.Should().Be("Strong Candidate");
        response.Data.Results[0].RankPosition.Should().Be(1);
        // Prose enriched from provider, machine order untouched. Unknown candidate not added.
        response.Data.Results.Should().HaveCount(2);
        response.Data.Results[0].Summary.Should().Be("AI Strong tóm tắt");
        response.Data.Warnings.Should().Contain("provider-order:ignored");
        response.Data.Warnings.Should().Contain("provider-candidate:unknown");
    }

    [Fact]
    public async Task CreateRankingAsync_WhenInputUnchanged_ReusesSessionWithoutProviderOrDuplicate()
    {
        // v2 §8 — unchanged effective input returns the latest matching session; no provider call, no
        // duplicate session persisted.
        var repository = new Mock<ICopilotRepository>();
        var aiProvider = new Mock<IAiCopilotProvider>();
        Guid jobId = Guid.NewGuid();
        Guid ownerId = Guid.NewGuid();
        Guid conversationId = Guid.NewGuid();

        var pool = BuildCopilotPool(jobId);
        repository.Setup(v => v.GetConversationAsync(conversationId)).ReturnsAsync(new CopilotConversation { Id = conversationId, JobId = jobId, UserId = ownerId });
        repository.Setup(v => v.GetCandidatePoolAsync(jobId)).ReturnsAsync(pool);
        repository.Setup(v => v.GetSavedRulesAsync(jobId, ownerId)).ReturnsAsync([]);
        repository.Setup(v => v.GetLatestMatchingRankingSessionAsync(jobId, ownerId, It.IsAny<string>())).ReturnsAsync(new CopilotRankingSession
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            UserId = ownerId,
            NormalizedRulesJson = "{}",
            Results = []
        });

        CopilotService service = CreateService(repository.Object, aiProvider: aiProvider.Object,
            aiSettings: new AiProviderSettings { Enabled = true, ApiKey = "test-key", Model = "m" });

        var response = await service.CreateRankingAsync(conversationId, new CopilotPromptRequest
        {
            JobId = jobId,
            Prompt = "Xếp hạng ứng viên",
            ForceRanking = true
        }, ownerId);

        response.Success.Should().BeTrue();
        response.Data!.ReusedRankingSession.Should().BeTrue();
        response.Data.Warnings.Should().Contain("ranking-session:reused");
        response.Data.AssistantMessage.Should().Contain("Tiêu chí chưa thay đổi");
        aiProvider.Verify(v => v.TryCreateRankingAsync(It.IsAny<CopilotCandidatePoolDto>(), It.IsAny<CopilotNormalizedRulesDto>(), It.IsAny<IReadOnlyList<CopilotRankingResultDto>>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(v => v.AddRankingSessionAsync(It.IsAny<CopilotRankingSession>()), Times.Never);
    }

    [Fact]
    public async Task CreateRankingAsync_UnrelatedChatPrompt_RefusesWithoutPoolOrProvider()
    {
        // v2 §14 — an unrelated chat prompt is refused in Vietnamese without loading the pool or
        // calling the AI provider.
        var repository = new Mock<ICopilotRepository>();
        var aiProvider = new Mock<IAiCopilotProvider>();
        Guid jobId = Guid.NewGuid();
        Guid ownerId = Guid.NewGuid();
        Guid conversationId = Guid.NewGuid();

        repository.Setup(v => v.GetConversationAsync(conversationId)).ReturnsAsync(new CopilotConversation { Id = conversationId, JobId = jobId, UserId = ownerId });
        repository.Setup(v => v.GetNextMessageSequenceAsync(conversationId)).ReturnsAsync(1);

        CopilotService service = CreateService(repository.Object, aiProvider: aiProvider.Object);

        var response = await service.CreateRankingAsync(conversationId, new CopilotPromptRequest
        {
            JobId = jobId,
            Prompt = "Thời tiết hôm nay thế nào?",
            ForceRanking = false
        }, ownerId);

        response.Success.Should().BeTrue();
        response.Data!.DidRank.Should().BeFalse();
        response.Data.AssistantMessage.Should().Contain("Đây không phải nhiệm vụ của tôi");
        repository.Verify(v => v.GetCandidatePoolAsync(It.IsAny<Guid>()), Times.Never);
        aiProvider.Verify(v => v.TryCreateChatReplyAsync(It.IsAny<CopilotCandidatePoolDto>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PassCvToHeadReviewAsync_MovesScreeningToHeadReview_SkipsNonScreening()
    {
        // v2 §7 — explicit HR action moves Screening -> ManagerReview; a non-Screening application is
        // skipped with a clear reason.
        var repository = new Mock<ICopilotRepository>();
        var applicationRepository = new Mock<IApplicationRepository>();
        var applicationService = new Mock<IApplicationService>();
        Guid jobId = Guid.NewGuid();
        Guid ownerId = Guid.NewGuid();
        Guid sessionId = Guid.NewGuid();
        Guid screeningApp = Guid.NewGuid();
        Guid headReviewApp = Guid.NewGuid();

        repository.Setup(v => v.GetRankingSessionAsync(sessionId)).ReturnsAsync(new CopilotRankingSession
        {
            Id = sessionId,
            JobId = jobId,
            UserId = ownerId,
            NormalizedRulesJson = "{}",
            Results =
            [
                new CopilotRankingResult { ApplicationId = screeningApp, CandidateUserId = Guid.NewGuid() },
                new CopilotRankingResult { ApplicationId = headReviewApp, CandidateUserId = Guid.NewGuid() }
            ]
        });
        applicationRepository.Setup(v => v.GetByIdAsync(screeningApp)).ReturnsAsync(new Domain.Entities.Application { Id = screeningApp, JobId = jobId, Status = ApplicationStatus.Screening });
        applicationRepository.Setup(v => v.GetByIdAsync(headReviewApp)).ReturnsAsync(new Domain.Entities.Application { Id = headReviewApp, JobId = jobId, Status = ApplicationStatus.ManagerReview });
        applicationService.Setup(v => v.UpdateApplicationDecisionAsync(screeningApp.ToString(), ownerId, It.IsAny<UpdateApplicationDecisionRequest>()))
            .ReturnsAsync(ApiResponse<ApplicationReviewDetailDto>.Ok(new ApplicationReviewDetailDto(), "moved"));

        CopilotService service = CreateService(repository.Object, applicationRepository: applicationRepository.Object, applicationService: applicationService.Object);

        var response = await service.PassCvToHeadReviewAsync(sessionId, new PassCvToHeadReviewRequest
        {
            ApplicationIds = [screeningApp, headReviewApp]
        }, ownerId, ["HR"]);

        response.Success.Should().BeTrue();
        response.Data!.Updated.Should().ContainSingle(u => u.ApplicationId == screeningApp && u.NewStatus == "ManagerReview");
        response.Data.Skipped.Should().ContainSingle(s => s.ApplicationId == headReviewApp);
        applicationService.Verify(v => v.UpdateApplicationDecisionAsync(headReviewApp.ToString(), It.IsAny<Guid?>(), It.IsAny<UpdateApplicationDecisionRequest>()), Times.Never);
    }

    private static CopilotService CreateService(
        ICopilotRepository? repository = null,
        IJobRepository? jobRepository = null,
        IApplicationRepository? applicationRepository = null,
        IAiCopilotProvider? aiProvider = null,
        AiProviderSettings? aiSettings = null,
        IApplicationService? applicationService = null)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(value => value.SaveChangesAsync()).ReturnsAsync(1);

        return new CopilotService(
            repository ?? Mock.Of<ICopilotRepository>(),
            jobRepository ?? Mock.Of<IJobRepository>(),
            applicationRepository ?? Mock.Of<IApplicationRepository>(),
            applicationService ?? Mock.Of<IApplicationService>(),
            Mock.Of<IFileStorageService>(),
            Mock.Of<IResumeTextExtractor>(),
            aiProvider ?? Mock.Of<IAiCopilotProvider>(),
            unitOfWork.Object,
            Options.Create(aiSettings ?? new AiProviderSettings()),
            TestMapperFactory.Create());
    }

    private static CopilotCandidatePoolDto BuildCopilotPool(Guid jobId)
    {
        return new CopilotCandidatePoolDto
        {
            Job = new CopilotJobContextDto
            {
                JobId = jobId,
                Title = "Senior .NET Engineer",
                RequiredSkills = [".NET", "SQL"],
                Requirements = ["3+ years backend experience"]
            },
            Candidates =
            [
                new CopilotCandidateDto
                {
                    CandidateUserId = Guid.NewGuid(),
                    ApplicationId = Guid.NewGuid(),
                    FullName = "Strong Candidate",
                    ExperienceYears = 5,
                    Skills = [".NET", "SQL", "Azure"],
                    CvSummary = "Built .NET APIs with SQL and Azure.",
                    Status = "Screening"
                },
                new CopilotCandidateDto
                {
                    CandidateUserId = Guid.NewGuid(),
                    ApplicationId = Guid.NewGuid(),
                    FullName = "Junior Candidate",
                    ExperienceYears = 1,
                    Skills = ["JavaScript"],
                    CvSummary = "Frontend internship experience.",
                    Status = "Screening"
                }
            ]
        };
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
        jobRepository.Setup(value => value.GetPendingApprovalJobsAsync(5, It.IsAny<Guid?>())).ReturnsAsync([new Job { Id = Guid.NewGuid(), Title = "Pending", WorkMode = WorkMode.Remote, Department = new Department { Name = "Engineering" } }]);

        var service = new DashboardService(
            Mock.Of<ICandidateProfileRepository>(),
            Mock.Of<IUserRepository>(),
            applicationRepository.Object,
            jobRepository.Object,
            interviewRepository.Object,
            Mock.Of<INotificationRepository>(),
            Mock.Of<ISemanticDiscoveryService>(),
            Mock.Of<IUnitOfWork>());

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
            Mock.Of<INotificationEventService>(),
            Mock.Of<ILogger<InterviewService>>(),
            TestMapperFactory.Create());

        var response = await service.GetScheduleDataAsync("not-a-guid");

        response.StatusCode.Should().Be(400);
        response.ErrorCode.Should().Be(RecruitPro.Application.Common.ErrorCodes.InvalidInput);
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
            Mock.Of<INotificationEventService>(),
            Mock.Of<ILogger<InterviewService>>(),
            TestMapperFactory.Create());

        var response = await service.UpdateInterviewStatusAsync(interviewId.ToString(), new UpdateInterviewStatusRequest { Status = "weird" });

        response.StatusCode.Should().Be(400);
        response.ErrorCode.Should().Be(RecruitPro.Application.Common.ErrorCodes.InvalidInput);
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
            Mock.Of<IUserRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<ISemanticDiscoveryService>(),
            Mock.Of<INotificationEventService>(),
            Mock.Of<ILogger<JobService>>(),
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
            Mock.Of<IUserRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<ISemanticDiscoveryService>(),
            Mock.Of<INotificationEventService>(),
            Mock.Of<ILogger<JobService>>(),
            TestMapperFactory.Create());

        var response = await service.DeleteJobAsync("bad-id");

        response.StatusCode.Should().Be(404);
        response.ErrorCode.Should().Be(RecruitPro.Application.Common.ErrorCodes.JobNotFound);
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

public sealed class NotificationServiceUnitTests
{
    [Fact]
    public async Task GetUserNotificationsAsync_ClampsPagingAndMapsJsonData()
    {
        Guid userId = Guid.NewGuid();
        Guid notificationId = Guid.NewGuid();
        var repository = new Mock<INotificationRepository>();
        repository.Setup(value => value.GetByUserIdAsync(userId, 1, 50))
            .ReturnsAsync(
            [
                new Notification
                {
                    Id = notificationId,
                    UserId = userId,
                    EventCode = "candidate_score_ready",
                    Title = "Score ready",
                    Body = "Done",
                    DataJson = "{\"score\":92}",
                    EntityType = "application",
                    EntityId = Guid.NewGuid(),
                    Type = "APPLICATION",
                    IsRead = false,
                    CreatedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)
                }
            ]);
        repository.Setup(value => value.CountByUserIdAsync(userId)).ReturnsAsync(75);

        var service = new NotificationService(repository.Object);

        var response = await service.GetUserNotificationsAsync(userId, -3, 999);

        response.Success.Should().BeTrue();
        response.Data!.Items.Should().ContainSingle();
        response.Data.Items[0].Id.Should().Be(notificationId);
        response.Data.Items[0].IsRead.Should().BeFalse();
        response.Data.Items[0].Data.Should().BeOfType<JsonElement>()
            .Which.GetProperty("score").GetInt32().Should().Be(92);
        response.Data.Meta!.Page.Should().Be(1);
        response.Data.Meta.PageSize.Should().Be(50);
        response.Data.Meta.TotalItems.Should().Be(75);
    }

    [Fact]
    public async Task MarkAsReadAsync_WhenNotificationBelongsToUser_MarksAndReturnsNotification()
    {
        Guid userId = Guid.NewGuid();
        Guid notificationId = Guid.NewGuid();
        var notification = new Notification
        {
            Id = notificationId,
            UserId = userId,
            Title = "Interview",
            Body = "Scheduled",
            Type = "INTERVIEW",
            IsRead = false
        };
        var repository = new Mock<INotificationRepository>();
        repository.Setup(value => value.GetByIdAsync(notificationId)).ReturnsAsync(notification);

        var service = new NotificationService(repository.Object);

        var response = await service.MarkAsReadAsync(userId, notificationId.ToString());

        response.Success.Should().BeTrue();
        response.Data!.IsRead.Should().BeTrue();
        repository.Verify(value => value.MarkAsReadAsync(notificationId, It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task MarkAsReadAsync_WhenNotificationBelongsToAnotherUser_ReturnsNotFound()
    {
        var repository = new Mock<INotificationRepository>();
        repository.Setup(value => value.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new Notification { Id = Guid.NewGuid(), UserId = Guid.NewGuid() });

        var service = new NotificationService(repository.Object);

        var response = await service.MarkAsReadAsync(Guid.NewGuid(), Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(404);
        response.ErrorCode.Should().Be(RecruitPro.Application.Common.ErrorCodes.NotificationNotFound);
        repository.Verify(value => value.MarkAsReadAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Never);
    }
}

public sealed class NotificationEventServiceUnitTests
{
    [Fact]
    public async Task PublishApplicationStatusChangedAsync_WhenStatusUnchanged_DoesNotPersistOrSend()
    {
        var notificationRepository = new Mock<INotificationRepository>();
        var realtimeSender = new Mock<INotificationRealtimeSender>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var service = new NotificationEventService(
            notificationRepository.Object,
            Mock.Of<IUserRepository>(),
            realtimeSender.Object,
            unitOfWork.Object,
            Mock.Of<RecruitPro.Application.Interfaces.IServices.Automation.IRecruitProEventBus>());
        var application = new Domain.Entities.Application
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Status = ApplicationStatus.Screening,
            Job = new Job { Title = "Backend Engineer" }
        };

        await service.PublishApplicationStatusChangedAsync(application, ApplicationStatus.Screening);

        notificationRepository.Verify(value => value.AddRangeAsync(It.IsAny<IEnumerable<Notification>>()), Times.Never);
        unitOfWork.Verify(value => value.SaveChangesAsync(), Times.Never);
        realtimeSender.Verify(value => value.SendToUserAsync(
            It.IsAny<Guid>(),
            It.IsAny<NotificationDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
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
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IEmailService>(),
            Mock.Of<INotificationEventService>(),
            Mock.Of<ILogger<OfferService>>());

        var response = await service.GetOfferEditorAsync("not-a-guid", null, Array.Empty<string>());

        response.StatusCode.Should().Be(404);
        response.ErrorCode.Should().Be(RecruitPro.Application.Common.ErrorCodes.ApplicationNotFound);
    }

    [Fact]
    public async Task SaveDraftAsync_WhenCurrencyUnavailable_ReturnsBadRequest()
    {
        Guid applicationId = Guid.NewGuid();
        Guid actorId = Guid.NewGuid();
        var applicationRepository = new Mock<IApplicationRepository>();
        var offerRepository = new Mock<IOfferRepository>();
        var application = new Domain.Entities.Application
        {
            Id = applicationId,
            Status = ApplicationStatus.Offer,
            AssignedRecruiterId = actorId,
            User = new User { Username = "candidate.user", FullName = "Candidate", Email = "candidate@test.com" },
            Job = new Job { Title = "Backend", Department = new Department { Name = "Engineering" }, EmploymentType = EmploymentType.FullTime }
        };

        applicationRepository.Setup(value => value.GetTrackedByIdAsync(applicationId)).ReturnsAsync(application);
        offerRepository.Setup(value => value.GetCurrenciesAsync()).ReturnsAsync([]);

        var service = new OfferService(
            applicationRepository.Object,
            offerRepository.Object,
            Mock.Of<IUserRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IEmailService>(),
            Mock.Of<INotificationEventService>(),
            Mock.Of<ILogger<OfferService>>());

        var response = await service.SaveDraftAsync(applicationId.ToString(), actorId, new[] { "HR" }, new UpsertApplicationOfferRequest
        {
            CurrencyCode = "USD",
            EmploymentType = "Full-time"
        });

        response.StatusCode.Should().Be(400);
        response.ErrorCode.Should().Be(RecruitPro.Application.Common.ErrorCodes.InvalidInput);
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
        response.ErrorCode.Should().Be(RecruitPro.Application.Common.ErrorCodes.InvalidInput);
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

        await act.Should().ThrowAsync<BusinessAppException>()
            .Where(e => e.Code == RecruitPro.Application.Common.ErrorCodes.CandidateProfileNotFound);
    }
}

public sealed class CopilotMappingTests
{
    [Fact] // Regression: the Copilot criterion map must be registered and valid (the missing map = 500).
    public void SharedProfile_RegistersCopilotCriterionMap_AndItIsValid()
    {
        IMapper mapper = TestMapperFactory.Create();

        // Mapping a criterion must not throw the missing-map exception (the original 500 root cause),
        // and must produce a populated destination.
        CopilotRuleCriterionDto? mapped = null;
        mapper.Invoking(value => mapped = value.Map<CopilotRuleCriterionDto>(new CopilotRuleCriterionRequestDto { Value = "react" }))
            .Should().NotThrow();
        mapped.Should().NotBeNull();
        mapped!.Value.Should().Be("react");
    }

    [Fact] // Ranking criteria map carries every field (Label/Field/Operator/Value/Weight/AutoReject).
    public void CopilotRuleCriterionRequest_MapsToResponseDto_WithAllFields()
    {
        IMapper mapper = TestMapperFactory.Create();
        var request = new CopilotRuleCriterionRequestDto
        {
            Label = "Must have React",
            Field = "skill",
            Operator = "contains",
            Value = "react",
            Weight = "high",
            AutoReject = true
        };

        CopilotRuleCriterionDto dto = mapper.Map<CopilotRuleCriterionDto>(request);

        dto.Label.Should().Be("Must have React");
        dto.Field.Should().Be("skill");
        dto.Operator.Should().Be("contains");
        dto.Value.Should().Be("react");
        dto.Weight.Should().Be("high");
        dto.AutoReject.Should().BeTrue();
    }

    [Fact] // Ranking with multiple criteria maps the full list without throwing.
    public void CopilotRuleCriterionRequest_ListMapping_PreservesAllItems()
    {
        IMapper mapper = TestMapperFactory.Create();
        List<CopilotRuleCriterionRequestDto> requests =
        [
            new() { Label = "A", Field = "skill", Value = "react", Weight = "high" },
            new() { Label = "B", Field = "title", Value = "senior", Weight = "medium" }
        ];

        List<CopilotRuleCriterionDto> mapped = requests
            .Select(request => mapper.Map<CopilotRuleCriterionDto>(request))
            .ToList();

        mapped.Should().HaveCount(2);
        mapped.Select(item => item.Value).Should().ContainInOrder("react", "senior");
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
        }, NullLoggerFactory.Instance);
        return configuration.CreateMapper();
    });

    public static IMapper Create() => Mapper.Value;
}
