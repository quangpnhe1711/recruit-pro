using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.DTOs.Response.Copilot;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Infrastructure.Data;

namespace RecruitPro.Tests.Infrastructure;

public sealed class RecruitProWebApplicationFactory(PostgresTestFixture fixture) : WebApplicationFactory<Program>, IAsyncLifetime
{
    public PostgresTestFixture Fixture { get; } = fixture;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Mycnn"] = Fixture.ConnectionString,
                ["AiProvider:Enabled"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<IFileStorageService>();
            services.RemoveAll<IEmailService>();
            services.RemoveAll<IResumeTextExtractor>();
            services.RemoveAll<IResumeParsingAiProvider>();
            services.RemoveAll<IEmbeddingProvider>();
            services.RemoveAll<IAiCopilotProvider>();

            var hostedServices = services
                .Where(descriptor => descriptor.ServiceType == typeof(IHostedService)
                    && descriptor.ImplementationType == typeof(SemanticScoringBackgroundService))
                .ToList();
            foreach (var hostedService in hostedServices)
            {
                services.Remove(hostedService);
            }

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(Fixture.ConnectionString)
                    .EnableSensitiveDataLogging());

            services.AddSingleton<IFileStorageService, FakeFileStorageService>();
            services.AddSingleton<IEmailService, FakeEmailService>();
            services.AddSingleton<IResumeTextExtractor, FakeResumeTextExtractor>();
            services.AddSingleton<IResumeParsingAiProvider, FakeResumeParsingAiProvider>();
            services.AddSingleton<IEmbeddingProvider, FakeEmbeddingProvider>();
            services.AddSingleton<IAiCopilotProvider, FakeAiCopilotProvider>();
        });
    }

    public async Task InitializeAsync()
    {
        await Fixture.ResetDatabaseAsync(Services);
    }

    public new Task DisposeAsync() => Task.CompletedTask;
}

internal sealed class FakeFileStorageService : IFileStorageService
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new();

    public Task<string> UploadFileAsync(Stream stream, string objectName, string contentType)
    {
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        _files[objectName] = buffer.ToArray();
        return Task.FromResult(objectName);
    }

    public Task<string> GetPresignedUrlAsync(string objectName) => Task.FromResult($"https://files.test/{objectName}");

    public Task<Stream> DownloadFileAsync(string objectName)
    {
        byte[] bytes = _files.TryGetValue(objectName, out byte[]? stored) ? stored : [];
        return Task.FromResult<Stream>(new MemoryStream(bytes));
    }

    public Task DeleteFileAsync(string objectName)
    {
        _files.TryRemove(objectName, out _);
        return Task.CompletedTask;
    }
}

internal sealed class FakeEmailService : IEmailService
{
    public Task SendCandidateInvitationAsync(string email, string fullName, string temporaryPassword, string loginUrl) => Task.CompletedTask;
    public Task SendPasswordResetAsync(string email, string fullName, string temporaryPassword, string loginUrl) => Task.CompletedTask;
    public Task SendOfferEmailAsync(string email, string fullName, string jobTitle, string subject, string body) => Task.CompletedTask;
    public Task SendRejectionEmailAsync(string email, string fullName, string jobTitle, string subject, string body) => Task.CompletedTask;
}

internal sealed class FakeResumeTextExtractor : IResumeTextExtractor
{
    public Task<string> ExtractTextAsync(Stream resumeStream, CancellationToken cancellationToken = default)
        => Task.FromResult("Backend engineer with PostgreSQL and .NET experience over four years.");
}

internal sealed class FakeResumeParsingAiProvider : IResumeParsingAiProvider
{
    public Task<ResumeParsingAiResult> TryParseResumeAsync(string extractedText, IReadOnlyList<RecruitPro.Domain.Entities.Skill> availableSkills, CancellationToken cancellationToken = default)
        => Task.FromResult(new ResumeParsingAiResult { UsedAi = false, FailureReason = "Disabled in tests" });
}

internal sealed class FakeEmbeddingProvider : IEmbeddingProvider
{
    public Task<EmbeddingGenerationResult> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        => Task.FromResult(new EmbeddingGenerationResult { Succeeded = true, Vector = [0.1, 0.2, 0.3] });
}

internal sealed class FakeAiCopilotProvider : IAiCopilotProvider
{
    public Task<AiStructuredJsonResult> TryCreateStructuredJsonAsync(string actionType, string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
        => Task.FromResult(AiStructuredJsonResult.Failure("Disabled in tests", "fake-ai", "fake-model"));

    public Task<CopilotPromptResponseDto?> TryCreateRankingAsync(CopilotCandidatePoolDto pool, CopilotNormalizedRulesDto rules, IReadOnlyList<CopilotRankingResultDto> deterministicResults, string userPrompt, Guid conversationId, CancellationToken cancellationToken = default)
        => Task.FromResult<CopilotPromptResponseDto?>(null);

    public Task<string?> TryCreateChatReplyAsync(CopilotCandidatePoolDto pool, string userPrompt, Guid conversationId, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>("Test reply");
}
