using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruitPro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserUsername : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "username",
                table: "users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql(
                """
                WITH normalized AS (
                    SELECT
                        id,
                        COALESCE(
                            NULLIF(
                                trim(BOTH '-.' FROM lower(regexp_replace(split_part(email, '@', 1), '[^a-z0-9._-]', '-', 'g'))),
                                ''
                            ),
                            'user'
                        ) AS base_username
                    FROM users
                ),
                ranked AS (
                    SELECT
                        id,
                        base_username,
                        row_number() OVER (PARTITION BY base_username ORDER BY id) AS row_no
                    FROM normalized
                )
                UPDATE users AS u
                SET username = CASE
                    WHEN ranked.row_no = 1 THEN
                        left(
                            CASE
                                WHEN length(ranked.base_username) >= 4 THEN ranked.base_username
                                ELSE ranked.base_username || '-user'
                            END,
                            50
                        )
                    ELSE
                        left(
                            CASE
                                WHEN length(ranked.base_username) >= 4 THEN ranked.base_username
                                ELSE ranked.base_username || '-user'
                            END,
                            43
                        ) || '-' || ranked.row_no
                END
                FROM ranked
                WHERE u.id = ranked.id;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "username",
                table: "users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "users_username_key",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "users_username_key",
                table: "users");

            migrationBuilder.DropColumn(
                name: "username",
                table: "users");
        }
    }
}
