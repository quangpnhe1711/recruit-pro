using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruitPro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCopilotV2Artifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS copilot_prompt_templates (
                    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                    owner_user_id uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                    name character varying(200) NOT NULL,
                    template_type character varying(60) NOT NULL,
                    prompt text NOT NULL,
                    is_active boolean NOT NULL DEFAULT true,
                    created_at timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    updated_at timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP
                );

                CREATE INDEX IF NOT EXISTS ix_copilot_prompt_templates_owner_type_active
                    ON copilot_prompt_templates(owner_user_id, template_type, is_active);

                CREATE TABLE IF NOT EXISTS candidate_fit_analyses (
                    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                    audit_id uuid NOT NULL,
                    job_id uuid NOT NULL REFERENCES jobs(id) ON DELETE CASCADE,
                    candidate_user_id uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                    application_id uuid NOT NULL REFERENCES applications(id) ON DELETE CASCADE,
                    fit_label character varying(40) NOT NULL,
                    confidence_score numeric(5,2) NOT NULL,
                    total_score numeric(5,2) NOT NULL,
                    strengths_json jsonb NOT NULL DEFAULT '[]'::jsonb,
                    gaps_json jsonb NOT NULL DEFAULT '[]'::jsonb,
                    evidence_json jsonb NOT NULL DEFAULT '[]'::jsonb,
                    summary text NOT NULL,
                    provider_name character varying(100) NOT NULL,
                    model_name character varying(100) NOT NULL,
                    fallback_used boolean NOT NULL DEFAULT false,
                    created_at timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP
                );

                CREATE INDEX IF NOT EXISTS ix_candidate_fit_analyses_job_candidate_created
                    ON candidate_fit_analyses(job_id, candidate_user_id, created_at);
                CREATE INDEX IF NOT EXISTS ix_candidate_fit_analyses_audit_id
                    ON candidate_fit_analyses(audit_id);

                CREATE TABLE IF NOT EXISTS copilot_generated_artifacts (
                    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                    owner_user_id uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                    job_id uuid NULL REFERENCES jobs(id) ON DELETE SET NULL,
                    application_id uuid NULL REFERENCES applications(id) ON DELETE SET NULL,
                    artifact_type character varying(60) NOT NULL,
                    prompt text NOT NULL DEFAULT '',
                    payload_json jsonb NOT NULL DEFAULT '{}'::jsonb,
                    provider_name character varying(100) NOT NULL,
                    model_name character varying(100) NOT NULL,
                    fallback_used boolean NOT NULL DEFAULT false,
                    created_at timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP
                );

                CREATE INDEX IF NOT EXISTS ix_copilot_generated_artifacts_owner_type_created
                    ON copilot_generated_artifacts(owner_user_id, artifact_type, created_at);
                CREATE INDEX IF NOT EXISTS ix_copilot_generated_artifacts_job_created
                    ON copilot_generated_artifacts(job_id, created_at);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS ix_copilot_generated_artifacts_job_created;
                DROP INDEX IF EXISTS ix_copilot_generated_artifacts_owner_type_created;
                DROP INDEX IF EXISTS ix_candidate_fit_analyses_audit_id;
                DROP INDEX IF EXISTS ix_candidate_fit_analyses_job_candidate_created;
                DROP INDEX IF EXISTS ix_copilot_prompt_templates_owner_type_active;

                DROP TABLE IF EXISTS copilot_generated_artifacts;
                DROP TABLE IF EXISTS candidate_fit_analyses;
                DROP TABLE IF EXISTS copilot_prompt_templates;
                """);
        }
    }
}
