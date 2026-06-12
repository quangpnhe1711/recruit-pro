using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruitPro.Infrastructure.Migrations
{
    public partial class AddApplicationCoverLetter : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cover_letter",
                table: "applications",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cover_letter",
                table: "applications");
        }
    }
}
