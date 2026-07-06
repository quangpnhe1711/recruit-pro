using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RecruitPro.Domain.Constants;
using RecruitPro.Domain.Entities;
using RecruitPro.Infrastructure.Data;

namespace RecruitPro.API.Seeding;

/// <summary>
/// v5 demo data seeder (Development only). Idempotent: each section is skipped if its table already has
/// rows, so restarts never duplicate. Produces a realistic-but-not-absurd spread of AI telemetry across
/// 2026-06-25 → 2026-07-04, plus prompt versions (with rollback history), provider routing policies and
/// evaluation cases, so the AI Ops dashboards render meaningfully without a live provider key. Uses a
/// FIXED base date and a seeded RNG so the demo is reproducible. Never runs in Production/Testing.
/// </summary>
public class V5DemoSeederHostedService : IHostedService
{
    // Base "today" for v5 demo data — kept in sync with the ATS seed in init.sql.
    private static readonly DateTime BaseDate = new(2026, 7, 4, 9, 0, 0, DateTimeKind.Unspecified);
    private const string ProviderHost = "generativelanguage.googleapis.com";
    private const string ChatModel = "gemini-3.1-flash-lite";
    private const string EmbeddingModel = "text-embedding-004";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<V5DemoSeederHostedService> _logger;

    public V5DemoSeederHostedService(IServiceScopeFactory scopeFactory, ILogger<V5DemoSeederHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await SeedPromptVersionsAsync(db, cancellationToken);
            await SeedProviderRoutingAsync(db, cancellationToken);
            await SeedEvaluationCasesAsync(db, cancellationToken);
            await SeedTelemetryAsync(db, cancellationToken);
        }
        catch (Exception exception)
        {
            // Best-effort: a seeding failure (e.g. DB not provisioned yet) must never block startup.
            _logger.LogWarning(exception, "v5 demo seeding skipped due to an error.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedPromptVersionsAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.PromptTemplateVersions.AnyAsync(ct))
        {
            return;
        }

        List<PromptTemplateVersion> versions = new();
        foreach ((string feature, string name, string body1, string body2) in PromptSeeds())
        {
            versions.Add(new PromptTemplateVersion
            {
                Id = Guid.NewGuid(),
                FeatureKey = feature,
                VersionNo = 1,
                Name = $"{name} v1",
                Description = "Initial baseline prompt.",
                TemplateBody = body1,
                VariablesJson = JsonSerializer.Serialize(new[] { "job", "candidate", "criteria" }, JsonOptions),
                IsActive = false,
                CreatedAt = BaseDate.AddDays(-9),
                Notes = "Baseline.",
            });
            versions.Add(new PromptTemplateVersion
            {
                Id = Guid.NewGuid(),
                FeatureKey = feature,
                VersionNo = 2,
                Name = $"{name} v2",
                Description = "Refined prompt: stricter evidence grounding + Vietnamese prose.",
                TemplateBody = body2,
                VariablesJson = JsonSerializer.Serialize(new[] { "job", "candidate", "criteria" }, JsonOptions),
                IsActive = true,
                CreatedAt = BaseDate.AddDays(-4),
                ActivatedAt = BaseDate.AddDays(-4),
                Notes = "Activated after evaluation showed better grounding.",
            });
        }

        await db.PromptTemplateVersions.AddRangeAsync(versions, ct);
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("v5 demo: seeded {Count} prompt template versions.", versions.Count);
    }

    private async Task SeedProviderRoutingAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.ProviderRoutingPolicies.AnyAsync(ct))
        {
            return;
        }

        (string feature, string model)[] policies =
        [
            (AiFeatureKeys.ResumeParsing, ChatModel),
            (AiFeatureKeys.CandidateFitAnalysis, ChatModel),
            (AiFeatureKeys.Ranking, ChatModel),
            (AiFeatureKeys.InterviewQuestions, ChatModel),
            (AiFeatureKeys.Embedding, EmbeddingModel),
            (AiFeatureKeys.SemanticScoring, EmbeddingModel),
        ];

        List<ProviderRoutingPolicy> rows = policies.Select(p => new ProviderRoutingPolicy
        {
            Id = Guid.NewGuid(),
            FeatureKey = p.feature,
            PrimaryProvider = "gemini",
            PrimaryModel = p.model,
            FallbackProvider = "deterministic",
            FallbackModel = "rule-based",
            IsEnabled = true,
            MaxLatencyMs = p.model == EmbeddingModel ? 1000 : 8000,
            MaxEstimatedCostUsd = 0.05m,
            CreatedAt = BaseDate.AddDays(-9),
            UpdatedAt = BaseDate.AddDays(-4),
        }).ToList();

        await db.ProviderRoutingPolicies.AddRangeAsync(rows, ct);
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("v5 demo: seeded {Count} provider routing policies.", rows.Count);
    }

    private async Task SeedEvaluationCasesAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.AiEvaluationCases.AnyAsync(ct))
        {
            return;
        }

        List<AiEvaluationCase> cases =
        [
            EvalCase(AiFeatureKeys.CandidateFitAnalysis, "Backend candidate — strong fit",
                new { job = "Senior .NET Backend", requiredSkills = new[] { "C#", "PostgreSQL", "EF Core" }, candidate = new { skills = new[] { "C#", "PostgreSQL", "EF Core", "Docker" }, years = 6 } },
                new { fitLabel = "high", minScore = 80 }),
            EvalCase(AiFeatureKeys.CandidateFitAnalysis, "Frontend candidate — medium fit",
                new { job = "React Frontend", requiredSkills = new[] { "React", "TypeScript" }, candidate = new { skills = new[] { "React", "JavaScript" }, years = 3 } },
                new { fitLabel = "medium", minScore = 55 }),
            EvalCase(AiFeatureKeys.CandidateFitAnalysis, "Candidate missing required skill",
                new { job = "Data Engineer", requiredSkills = new[] { "Spark", "Python", "SQL" }, candidate = new { skills = new[] { "Excel", "SQL" }, years = 2 } },
                new { fitLabel = "low", maxScore = 45 }),
            EvalCase(AiFeatureKeys.ResumeParsing, "Resume parsing — clean sample",
                new { resumeText = "Nguyen Van A. Senior Backend Engineer. Skills: C#, .NET, PostgreSQL. 6 years experience." },
                new { minSkills = 3, requiresName = true }),
        ];

        await db.AiEvaluationCases.AddRangeAsync(cases, ct);
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("v5 demo: seeded {Count} evaluation cases.", cases.Count);
    }

    private async Task SeedTelemetryAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.AiRunTelemetries.AnyAsync(ct))
        {
            return;
        }

        // Deterministic RNG so the demo is reproducible across restarts/environments.
        Random rng = new(20260704);
        List<AiRunTelemetry> rows = new();

        // Per-feature daily volume + behavior profile. Distributions are hand-tuned to look realistic:
        // ranking is frequent with high success, resume parsing has occasional schema failures, embedding
        // is high-volume + low-latency, fit analysis has a few AI-unavailable fallbacks.
        FeatureProfile[] profiles =
        [
            new(AiFeatureKeys.Ranking, ChatModel, 8, 15, 400, 1600, successRate: 0.93, fallbackRate: 0.06, schemaFailRate: 0.0, tokens: true),
            new(AiFeatureKeys.CandidateFitAnalysis, ChatModel, 5, 10, 350, 1300, successRate: 0.90, fallbackRate: 0.10, schemaFailRate: 0.0, tokens: true),
            new(AiFeatureKeys.ResumeParsing, ChatModel, 4, 9, 500, 2200, successRate: 0.88, fallbackRate: 0.08, schemaFailRate: 0.06, tokens: true),
            new(AiFeatureKeys.InterviewQuestions, ChatModel, 2, 6, 400, 1500, successRate: 0.95, fallbackRate: 0.03, schemaFailRate: 0.01, tokens: true),
            new(AiFeatureKeys.Embedding, EmbeddingModel, 20, 45, 40, 220, successRate: 0.985, fallbackRate: 0.0, schemaFailRate: 0.0, tokens: true),
            new(AiFeatureKeys.SemanticScoring, EmbeddingModel, 10, 22, 60, 300, successRate: 0.97, fallbackRate: 0.0, schemaFailRate: 0.0, tokens: true),
        ];

        for (int dayOffset = 9; dayOffset >= 0; dayOffset--)
        {
            DateTime day = BaseDate.AddDays(-dayOffset);
            foreach (FeatureProfile p in profiles)
            {
                int count = rng.Next(p.MinPerDay, p.MaxPerDay + 1);
                for (int i = 0; i < count; i++)
                {
                    rows.Add(BuildTelemetryRow(rng, p, day));
                }
            }
        }

        // Persist in batches to keep the insert reasonable.
        const int batchSize = 200;
        for (int offset = 0; offset < rows.Count; offset += batchSize)
        {
            await db.AiRunTelemetries.AddRangeAsync(rows.Skip(offset).Take(batchSize), ct);
            await db.SaveChangesAsync(ct);
        }

        _logger.LogInformation("v5 demo: seeded {Count} AI telemetry rows ({From:yyyy-MM-dd} → {To:yyyy-MM-dd}).",
            rows.Count, BaseDate.AddDays(-9), BaseDate);
    }

    private AiRunTelemetry BuildTelemetryRow(Random rng, FeatureProfile p, DateTime day)
    {
        double roll = rng.NextDouble();
        bool fallback = roll < p.FallbackRate;
        bool schemaFail = !fallback && roll < p.FallbackRate + p.SchemaFailRate;
        bool success = !fallback && !schemaFail && rng.NextDouble() < p.SuccessRate;

        // Failed/fallback rows tend to be slower (timeouts) — bias latency upward for them.
        int latency = rng.Next(p.MinLatency, p.MaxLatency + 1);
        if (!success)
        {
            latency = Math.Min(p.MaxLatency * 2, latency + rng.Next(500, 1500));
        }

        int? promptTokens = null, completionTokens = null, totalTokens = null;
        decimal? cost = null;
        if (p.HasTokens && (success || schemaFail))
        {
            if (p.Model == EmbeddingModel)
            {
                promptTokens = rng.Next(200, 900);
                totalTokens = promptTokens;
                cost = 0m; // embedding pricing modelled as free-tier in the demo pricing table
            }
            else
            {
                promptTokens = rng.Next(800, 3200);
                completionTokens = rng.Next(120, 900);
                totalTokens = promptTokens + completionTokens;
                cost = Math.Round((promptTokens.Value / 1_000_000m) * 0.10m + (completionTokens.Value / 1_000_000m) * 0.40m, 8);
            }
        }

        List<string> riskFlags = new();
        if (fallback)
        {
            riskFlags.Add(AiRiskFlags.FallbackUsed);
        }
        if (schemaFail)
        {
            riskFlags.Add(AiRiskFlags.SchemaInvalid);
        }
        if (!success && !fallback && !schemaFail)
        {
            riskFlags.Add(AiRiskFlags.ProviderError);
        }
        // Occasional content-quality flag on fit analysis to exercise the risk dashboard.
        if (success && p.Feature == AiFeatureKeys.CandidateFitAnalysis && rng.NextDouble() < 0.08)
        {
            riskFlags.Add(AiRiskFlags.HighConfidenceLowEvidence);
        }

        string? errorCode = success ? null : fallback ? "provider_failure" : schemaFail ? "schema_invalid" : "provider_error";
        string? errorMessage = success ? null
            : fallback ? "AI provider unavailable; deterministic fallback used."
            : schemaFail ? "AI response did not match the expected schema."
            : "AI provider returned an error.";

        DateTime createdAt = day
            .AddHours(rng.Next(0, 10))
            .AddMinutes(rng.Next(0, 60))
            .AddSeconds(rng.Next(0, 60));

        bool? schemaValid = p.Feature is AiFeatureKeys.Embedding or AiFeatureKeys.SemanticScoring
            ? null
            : success ? true : schemaFail ? false : (bool?)null;

        return new AiRunTelemetry
        {
            Id = Guid.NewGuid(),
            Feature = p.Feature,
            ProviderName = ProviderHost,
            ModelName = p.Model,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens = totalTokens,
            EstimatedCostUsd = cost,
            IsCostEstimated = true,
            LatencyMs = latency,
            Success = success,
            FallbackUsed = fallback,
            SchemaValid = schemaValid,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            CorrelationId = null,
            RiskFlagsJson = riskFlags.Count == 0 ? null : JsonSerializer.Serialize(riskFlags, JsonOptions),
            MetadataJson = JsonSerializer.Serialize(new { seeded = true }, JsonOptions),
            CreatedAt = createdAt,
        };
    }

    private static AiEvaluationCase EvalCase(string feature, string name, object input, object expected) => new()
    {
        Id = Guid.NewGuid(),
        FeatureKey = feature,
        Name = name,
        InputJson = JsonSerializer.Serialize(input, JsonOptions),
        ExpectedJson = JsonSerializer.Serialize(expected, JsonOptions),
        ScoringRubricJson = JsonSerializer.Serialize(new { criteria = "match expected fitLabel / skills", weight = 1.0 }, JsonOptions),
        IsActive = true,
        CreatedAt = BaseDate.AddDays(-6),
    };

    private static IEnumerable<(string Feature, string Name, string Body1, string Body2)> PromptSeeds()
    {
        yield return (
            AiFeatureKeys.CandidateFitAnalysis,
            "Candidate Fit Analysis",
            "Analyze the candidate fit for the job. Return JSON with fitLabel, confidence, strengths, gaps.",
            "Bạn là trợ lý tuyển dụng. Phân tích mức độ phù hợp của ứng viên với công việc dựa CHỈ trên bằng chứng trong hồ sơ. Trả về JSON gồm fitLabel, confidence, strengths, gaps, evidence. Không suy diễn thông tin không có trong hồ sơ.");
        yield return (
            AiFeatureKeys.ResumeParsing,
            "Resume Parsing",
            "Extract structured data from the resume. Return JSON.",
            "Trích xuất dữ liệu có cấu trúc từ CV thành JSON theo schema. Chỉ dùng thông tin có trong CV, dùng null khi thiếu. Ưu tiên tên kỹ năng khớp danh sách kỹ năng chuẩn.");
        yield return (
            "ranking_explanation",
            "Ranking Explanation",
            "Explain why the candidate is ranked at this position.",
            "Giải thích ngắn gọn (tiếng Việt) vì sao ứng viên được xếp ở vị trí này, nêu điểm mạnh khớp yêu cầu và điểm còn thiếu. Bám sát điểm số xác định (deterministic), không thay đổi thứ hạng.");
    }

    private sealed record FeatureProfile(
        string Feature, string Model, int MinPerDay, int MaxPerDay, int MinLatency, int MaxLatency,
        double successRate, double fallbackRate, double schemaFailRate, bool tokens)
    {
        public double SuccessRate => successRate;
        public double FallbackRate => fallbackRate;
        public double SchemaFailRate => schemaFailRate;
        public bool HasTokens => tokens;
    }
}
