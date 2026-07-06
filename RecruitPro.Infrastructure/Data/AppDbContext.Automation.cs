using Microsoft.EntityFrameworkCore;
using RecruitPro.Domain.Automation;

namespace RecruitPro.Infrastructure.Data;

/// <summary>
/// v4 Workflow Automation + MCP schema. Kept in a partial so the generated core context stays untouched.
/// Column types mirror db/patches/20260701-add-v4-*.sql exactly (snake_case, jsonb, enums as strings,
/// `timestamp without time zone` to match DbDateTime.Now). Integration tests build this via
/// EnsureCreated, so these mappings ARE the test schema.
/// </summary>
public partial class AppDbContext
{
    public virtual DbSet<PublishedDomainEvent> PublishedDomainEvents { get; set; }
    public virtual DbSet<WorkflowDefinition> WorkflowDefinitions { get; set; }
    public virtual DbSet<WorkflowDefinitionVersion> WorkflowDefinitionVersions { get; set; }
    public virtual DbSet<WorkflowExecution> WorkflowExecutions { get; set; }
    public virtual DbSet<WorkflowExecutionStep> WorkflowExecutionSteps { get; set; }
    public virtual DbSet<WorkflowActionDeadLetter> WorkflowActionDeadLetters { get; set; }
    public virtual DbSet<McpToolAudit> McpToolAudits { get; set; }
    public virtual DbSet<WorkerHeartbeat> WorkerHeartbeats { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PublishedDomainEvent>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("published_domain_events_pkey");
            entity.ToTable("published_domain_events");
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.EventType).HasMaxLength(100).HasColumnName("event_type");
            entity.Property(e => e.AggregateType).HasMaxLength(100).HasColumnName("aggregate_type");
            entity.Property(e => e.AggregateId).HasColumnName("aggregate_id");
            entity.Property(e => e.DedupKey).HasMaxLength(300).HasColumnName("dedup_key");
            entity.Property(e => e.PayloadJson).HasColumnType("jsonb").HasColumnName("payload_json");
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50).HasColumnName("status");
            entity.Property(e => e.OccurredAt).HasColumnType("timestamp without time zone").HasColumnName("occurred_at");
            entity.Property(e => e.ProcessedAt).HasColumnType("timestamp without time zone").HasColumnName("processed_at");
            entity.Property(e => e.NextAttemptAt).HasColumnType("timestamp without time zone").HasColumnName("next_attempt_at");
            entity.Property(e => e.AttemptCount).HasColumnName("attempt_count");
            entity.Property(e => e.ErrorReason).HasColumnName("error_reason");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");

            entity.HasIndex(e => e.DedupKey, "ux_published_domain_events_dedup_key").IsUnique();
            entity.HasIndex(e => new { e.Status, e.NextAttemptAt }, "ix_published_domain_events_status_next_attempt");
            entity.HasIndex(e => new { e.EventType, e.OccurredAt }, "ix_published_domain_events_event_type_occurred_at");
        });

        modelBuilder.Entity<WorkflowDefinition>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("workflow_definitions_pkey");
            entity.ToTable("workflow_definitions");
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.Name).HasMaxLength(200).HasColumnName("name");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsEnabled).HasDefaultValue(true).HasColumnName("is_enabled");
            entity.Property(e => e.ActiveVersionId).HasColumnName("active_version_id");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasColumnName("updated_at");

            entity.HasMany(e => e.Versions).WithOne(v => v.WorkflowDefinition)
                .HasForeignKey(v => v.WorkflowDefinitionId)
                .HasConstraintName("workflow_definition_versions_workflow_definition_id_fkey");

            // Read-only navigation to the active version; no FK constraint (avoids a cycle with Versions).
            entity.HasOne(e => e.ActiveVersion).WithMany()
                .HasForeignKey(e => e.ActiveVersionId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("workflow_definitions_active_version_id_fkey");
        });

        modelBuilder.Entity<WorkflowDefinitionVersion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("workflow_definition_versions_pkey");
            entity.ToTable("workflow_definition_versions");
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.WorkflowDefinitionId).HasColumnName("workflow_definition_id");
            entity.Property(e => e.VersionNo).HasColumnName("version_no");
            entity.Property(e => e.TriggerJson).HasColumnType("jsonb").HasColumnName("trigger_json");
            entity.Property(e => e.ConditionsJson).HasColumnType("jsonb").HasColumnName("conditions_json");
            entity.Property(e => e.ActionsJson).HasColumnType("jsonb").HasColumnName("actions_json");
            entity.Property(e => e.Mode).HasConversion<string>().HasMaxLength(50).HasColumnName("mode");
            entity.Property(e => e.IsActive).HasDefaultValue(false).HasColumnName("is_active");
            entity.Property(e => e.PublishedBy).HasColumnName("published_by");
            entity.Property(e => e.PublishedAt).HasColumnType("timestamp without time zone").HasColumnName("published_at");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");

            entity.HasIndex(e => new { e.WorkflowDefinitionId, e.VersionNo }, "ux_workflow_definition_versions_workflow_version").IsUnique();
        });

        modelBuilder.Entity<WorkflowExecution>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("workflow_executions_pkey");
            entity.ToTable("workflow_executions");
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.WorkflowDefinitionId).HasColumnName("workflow_definition_id");
            entity.Property(e => e.WorkflowDefinitionVersionId).HasColumnName("workflow_definition_version_id");
            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.EventDedupKey).HasMaxLength(300).HasColumnName("event_dedup_key");
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50).HasColumnName("status");
            entity.Property(e => e.Mode).HasConversion<string>().HasMaxLength(50).HasColumnName("mode");
            entity.Property(e => e.TriggerEventType).HasMaxLength(100).HasColumnName("trigger_event_type");
            entity.Property(e => e.InputPayloadJson).HasColumnType("jsonb").HasColumnName("input_payload_json");
            entity.Property(e => e.OutputJson).HasColumnType("jsonb").HasColumnName("output_json");
            entity.Property(e => e.ErrorReason).HasColumnName("error_reason");
            entity.Property(e => e.AttemptCount).HasColumnName("attempt_count");
            entity.Property(e => e.NextRetryAt).HasColumnType("timestamp without time zone").HasColumnName("next_retry_at");
            entity.Property(e => e.StartedAt).HasColumnType("timestamp without time zone").HasColumnName("started_at");
            entity.Property(e => e.FinishedAt).HasColumnType("timestamp without time zone").HasColumnName("finished_at");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");

            entity.HasMany(e => e.Steps).WithOne(s => s.Execution)
                .HasForeignKey(s => s.ExecutionId)
                .HasConstraintName("workflow_execution_steps_execution_id_fkey");

            // Idempotency: at most one execution per (version, event). Partial so ad-hoc/manual runs with
            // a null dedup key are not blocked.
            entity.HasIndex(e => new { e.WorkflowDefinitionVersionId, e.EventDedupKey }, "ux_workflow_executions_version_event")
                .IsUnique()
                .HasFilter("event_dedup_key IS NOT NULL");
            entity.HasIndex(e => new { e.Status, e.NextRetryAt }, "ix_workflow_executions_status_next_retry");
            entity.HasIndex(e => new { e.WorkflowDefinitionId, e.CreatedAt }, "ix_workflow_executions_definition_created");
        });

        modelBuilder.Entity<WorkflowExecutionStep>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("workflow_execution_steps_pkey");
            entity.ToTable("workflow_execution_steps");
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.ExecutionId).HasColumnName("execution_id");
            entity.Property(e => e.StepNo).HasColumnName("step_no");
            entity.Property(e => e.StepType).HasMaxLength(100).HasColumnName("step_type");
            entity.Property(e => e.ActionType).HasMaxLength(100).HasColumnName("action_type");
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50).HasColumnName("status");
            entity.Property(e => e.InputJson).HasColumnType("jsonb").HasColumnName("input_json");
            entity.Property(e => e.OutputJson).HasColumnType("jsonb").HasColumnName("output_json");
            entity.Property(e => e.ErrorReason).HasColumnName("error_reason");
            entity.Property(e => e.StartedAt).HasColumnType("timestamp without time zone").HasColumnName("started_at");
            entity.Property(e => e.FinishedAt).HasColumnType("timestamp without time zone").HasColumnName("finished_at");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");

            entity.HasIndex(e => new { e.ExecutionId, e.StepNo }, "ix_workflow_execution_steps_execution_step");
        });

        modelBuilder.Entity<WorkflowActionDeadLetter>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("workflow_action_dead_letters_pkey");
            entity.ToTable("workflow_action_dead_letters");
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.ExecutionId).HasColumnName("execution_id");
            entity.Property(e => e.StepId).HasColumnName("step_id");
            entity.Property(e => e.ActionType).HasMaxLength(100).HasColumnName("action_type");
            entity.Property(e => e.PayloadJson).HasColumnType("jsonb").HasColumnName("payload_json");
            entity.Property(e => e.ErrorReason).HasColumnName("error_reason");
            entity.Property(e => e.AttemptCount).HasColumnName("attempt_count");
            entity.Property(e => e.NextRetryAt).HasColumnType("timestamp without time zone").HasColumnName("next_retry_at");
            entity.Property(e => e.ResolvedAt).HasColumnType("timestamp without time zone").HasColumnName("resolved_at");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");

            entity.HasIndex(e => e.ExecutionId, "ix_workflow_action_dead_letters_execution_id");
        });

        modelBuilder.Entity<McpToolAudit>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("mcp_tool_audits_pkey");
            entity.ToTable("mcp_tool_audits");
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.ToolName).HasMaxLength(150).HasColumnName("tool_name");
            entity.Property(e => e.CallerUserId).HasColumnName("caller_user_id");
            entity.Property(e => e.InputJson).HasColumnType("jsonb").HasColumnName("input_json");
            entity.Property(e => e.OutputSummaryJson).HasColumnType("jsonb").HasColumnName("output_summary_json");
            entity.Property(e => e.Allowed).HasColumnName("allowed");
            entity.Property(e => e.DeniedReason).HasColumnName("denied_reason");
            entity.Property(e => e.LatencyMs).HasColumnName("latency_ms");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("created_at");

            entity.HasIndex(e => new { e.ToolName, e.CreatedAt }, "ix_mcp_tool_audits_tool_created");
        });

        modelBuilder.Entity<WorkerHeartbeat>(entity =>
        {
            entity.HasKey(e => e.WorkerName).HasName("workflow_worker_heartbeats_pkey");
            entity.ToTable("workflow_worker_heartbeats");
            entity.Property(e => e.WorkerName).HasMaxLength(100).HasColumnName("worker_name");
            entity.Property(e => e.LastBeatAt).HasColumnType("timestamp without time zone").HasColumnName("last_beat_at");
            entity.Property(e => e.Status).HasMaxLength(50).HasColumnName("status");
            entity.Property(e => e.Detail).HasColumnName("detail");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone").HasColumnName("updated_at");
        });

        // v5 AI Ops + Talent Intelligence schema (see AppDbContext.Ai.cs). Only one OnModelCreatingPartial
        // implementation is permitted, so the v5 mappings are invoked from here rather than a second hook.
        ConfigureAiOperations(modelBuilder);
    }
}
