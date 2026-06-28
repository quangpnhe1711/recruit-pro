using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruitPro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentHeadReviewRequestedAt : Migration
    {
        // Workflow-correctness phase: the DepartmentHead/Manager review queue must show the date HR sent
        // the application to Head Review (Screening -> ManagerReview), not the original applied date. This
        // adds the nullable timestamp column that records that hand-off.
        //
        // Raw SQL with IF NOT EXISTS so the migration is idempotent against databases whose schema was
        // created via EnsureCreated()/the model snapshot (which already builds the column) — matching the
        // pattern established by AddActiveApplicationUniqueIndex.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE applications
                ADD COLUMN IF NOT EXISTS department_head_review_requested_at timestamp without time zone NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE applications DROP COLUMN IF EXISTS department_head_review_requested_at;");
        }
    }
}
