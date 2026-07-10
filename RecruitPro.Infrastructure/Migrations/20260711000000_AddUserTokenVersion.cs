using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruitPro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserTokenVersion : Migration
    {
        // Account-deactivation enforcement: every issued access token embeds the user's TokenVersion, and
        // deactivating a user bumps it so all live tokens fail validation at once (JwtExtension.OnTokenValidated).
        //
        // Raw SQL with IF NOT EXISTS so the migration is idempotent against databases created via init.sql /
        // the model snapshot — matching the pattern established by AddDepartmentHeadReviewRequestedAt.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE users
                ADD COLUMN IF NOT EXISTS token_version integer NOT NULL DEFAULT 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE users DROP COLUMN IF EXISTS token_version;");
        }
    }
}
