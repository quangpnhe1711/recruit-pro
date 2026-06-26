using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruitPro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddActiveApplicationUniqueIndex : Migration
    {
        // NOTE: This migration is intentionally scoped to ONLY the active-application unique index
        // (INV-014). `dotnet ef migrations add` also surfaced pre-existing, un-migrated model drift on
        // the `notifications` table (body/data_json/entity_id/entity_type/event_code/type) and an
        // IX_applications_user_id change. That drift predates this work and is unrelated to the
        // application-workflow conformance pass, so it is deliberately excluded here to keep the
        // migration focused and to avoid a rollback that would drop unrelated columns. The model
        // snapshot reflects the true current model; that pre-existing drift is tracked separately.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use raw SQL with IF NOT EXISTS so the migration is idempotent against databases whose
            // schema was created via EnsureCreated()/the model snapshot (which already builds the index).
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS ux_applications_active_user_job
                ON applications (user_id, job_id)
                WHERE status IN ('Applied', 'Screening', 'ManagerReview', 'Interview', 'Offer');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_applications_active_user_job;");
        }
    }
}
