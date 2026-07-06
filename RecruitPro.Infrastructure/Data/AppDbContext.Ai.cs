using Microsoft.EntityFrameworkCore;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Infrastructure.Data;

/// <summary>
/// v5 AI Ops + Talent Intelligence schema (telemetry, prompt registry, provider routing, evaluation).
/// Kept in a partial so the scaffolded core context stays untouched. Column types mirror
/// db/patches/20260704-add-v5-ai-ops.sql exactly (snake_case, jsonb-as-string, enums/strings,
/// `timestamp without time zone` to match DbDateTime.Now). Integration tests build this via
/// EnsureCreated, so these mappings ARE the test schema. Wired from AppDbContext.Automation.cs's
/// OnModelCreatingPartial (only one partial-method implementation is allowed).
/// </summary>
public partial class AppDbContext
{
    public virtual DbSet<AiRunTelemetry> AiRunTelemetries { get; set; }
    public virtual DbSet<PromptTemplateVersion> PromptTemplateVersions { get; set; }
    public virtual DbSet<ProviderRoutingPolicy> ProviderRoutingPolicies { get; set; }
    public virtual DbSet<AiEvaluationCase> AiEvaluationCases { get; set; }
    public virtual DbSet<AiEvaluationResult> AiEvaluationResults { get; set; }

    internal void ConfigureAiOperations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AiRunTelemetry>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ai_run_telemetry_pkey");
            entity.ToTable("ai_run_telemetry");
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.Feature).HasMaxLength(100).HasColumnName("feature");
            entity.Property(e => e.ProviderName).HasMaxLength(100).HasColumnName("provider_name");
            entity.Property(e => e.ModelName).HasMaxLength(150).HasColumnName("model_name");
            entity.Property(e => e.PromptVersionId).HasColumnName("prompt_version_id");
            entity.Property(e => e.PromptTokens).HasColumnName("prompt_tokens");
            entity.Property(e => e.CompletionTokens).HasColumnName("completion_tokens");
            entity.Property(e => e.TotalTokens).HasColumnName("total_tokens");
            entity.Property(e => e.EstimatedCostUsd).HasPrecision(18, 8).HasColumnName("estimated_cost_usd");
            entity.Property(e => e.IsCostEstimated).HasDefaultValue(true).HasColumnName("is_cost_estimated");
            entity.Property(e => e.LatencyMs).HasColumnName("latency_ms");
            entity.Property(e => e.Success).HasColumnName("success");
            entity.Property(e => e.FallbackUsed).HasDefaultValue(false).HasColumnName("fallback_used");
            entity.Property(e => e.SchemaValid).HasColumnName("schema_valid");
            entity.Property(e => e.ErrorCode).HasMaxLength(100).HasColumnName("error_code");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
            entity.Property(e => e.CorrelationId).HasMaxLength(100).HasColumnName("correlation_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.WorkflowExecutionId).HasColumnName("workflow_execution_id");
            entity.Property(e => e.RiskFlagsJson).HasColumnType("jsonb").HasColumnName("risk_flags_json");
            entity.Property(e => e.MetadataJson).HasColumnType("jsonb").HasColumnName("metadata_json");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");

            entity.HasIndex(e => e.CreatedAt, "idx_ai_run_telemetry_created_at");
            entity.HasIndex(e => new { e.Feature, e.CreatedAt }, "idx_ai_run_telemetry_feature_created_at");
            entity.HasIndex(e => new { e.ProviderName, e.ModelName, e.CreatedAt }, "idx_ai_run_telemetry_provider_model_created_at");
            entity.HasIndex(e => new { e.Success, e.CreatedAt }, "idx_ai_run_telemetry_success_created_at");
            entity.HasIndex(e => e.CorrelationId, "idx_ai_run_telemetry_correlation_id");
        });

        modelBuilder.Entity<PromptTemplateVersion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("prompt_template_versions_pkey");
            entity.ToTable("prompt_template_versions");
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.FeatureKey).HasMaxLength(100).HasColumnName("feature_key");
            entity.Property(e => e.VersionNo).HasColumnName("version_no");
            entity.Property(e => e.Name).HasMaxLength(200).HasColumnName("name");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.TemplateBody).HasColumnName("template_body");
            entity.Property(e => e.VariablesJson).HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb").HasColumnName("variables_json");
            entity.Property(e => e.IsActive).HasDefaultValue(false).HasColumnName("is_active");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.ActivatedBy).HasColumnName("activated_by");
            entity.Property(e => e.ActivatedAt).HasColumnType("timestamp without time zone").HasColumnName("activated_at");
            entity.Property(e => e.Notes).HasColumnName("notes");

            entity.HasIndex(e => new { e.FeatureKey, e.VersionNo }, "ux_prompt_template_versions_feature_version").IsUnique();
            // At most one active version per feature. Partial unique index enforces it at the DB level.
            entity.HasIndex(e => e.FeatureKey, "ux_prompt_template_versions_active_feature")
                .IsUnique()
                .HasFilter("is_active");
        });

        modelBuilder.Entity<ProviderRoutingPolicy>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("provider_routing_policies_pkey");
            entity.ToTable("provider_routing_policies");
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.FeatureKey).HasMaxLength(100).HasColumnName("feature_key");
            entity.Property(e => e.PrimaryProvider).HasMaxLength(100).HasColumnName("primary_provider");
            entity.Property(e => e.PrimaryModel).HasMaxLength(150).HasColumnName("primary_model");
            entity.Property(e => e.FallbackProvider).HasMaxLength(100).HasColumnName("fallback_provider");
            entity.Property(e => e.FallbackModel).HasMaxLength(150).HasColumnName("fallback_model");
            entity.Property(e => e.IsEnabled).HasDefaultValue(true).HasColumnName("is_enabled");
            entity.Property(e => e.MaxLatencyMs).HasColumnName("max_latency_ms");
            entity.Property(e => e.MaxEstimatedCostUsd).HasPrecision(18, 8).HasColumnName("max_estimated_cost_usd");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasIndex(e => e.FeatureKey, "ux_provider_routing_policies_feature").IsUnique();
        });

        modelBuilder.Entity<AiEvaluationCase>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ai_evaluation_cases_pkey");
            entity.ToTable("ai_evaluation_cases");
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.FeatureKey).HasMaxLength(100).HasColumnName("feature_key");
            entity.Property(e => e.Name).HasMaxLength(200).HasColumnName("name");
            entity.Property(e => e.InputJson).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb").HasColumnName("input_json");
            entity.Property(e => e.ExpectedJson).HasColumnType("jsonb").HasColumnName("expected_json");
            entity.Property(e => e.ScoringRubricJson).HasColumnType("jsonb").HasColumnName("scoring_rubric_json");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");

            entity.HasIndex(e => new { e.FeatureKey, e.IsActive }, "idx_ai_evaluation_cases_feature_active");
        });

        modelBuilder.Entity<AiEvaluationResult>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ai_evaluation_results_pkey");
            entity.ToTable("ai_evaluation_results");
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.RunId).HasColumnName("run_id");
            entity.Property(e => e.CaseId).HasColumnName("case_id");
            entity.Property(e => e.PromptVersionId).HasColumnName("prompt_version_id");
            entity.Property(e => e.ProviderName).HasMaxLength(100).HasColumnName("provider_name");
            entity.Property(e => e.ModelName).HasMaxLength(150).HasColumnName("model_name");
            entity.Property(e => e.Score).HasPrecision(5, 2).HasColumnName("score");
            entity.Property(e => e.Passed).HasColumnName("passed");
            entity.Property(e => e.OutputJson).HasColumnType("jsonb").HasColumnName("output_json");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
            entity.Property(e => e.LatencyMs).HasColumnName("latency_ms");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");

            entity.HasIndex(e => new { e.RunId, e.CreatedAt }, "idx_ai_evaluation_results_run_created");
            entity.HasIndex(e => e.CaseId, "idx_ai_evaluation_results_case");

            // A result belongs to a case; deleting a case removes its results. prompt_version_id is a
            // loose reference (no FK) so an active prompt version can be rolled without cascade effects.
            entity.HasOne<AiEvaluationCase>().WithMany()
                .HasForeignKey(e => e.CaseId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("ai_evaluation_results_case_id_fkey");
        });
    }
}
