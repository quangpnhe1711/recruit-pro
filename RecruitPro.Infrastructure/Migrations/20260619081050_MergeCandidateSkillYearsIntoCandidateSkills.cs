using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruitPro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MergeCandidateSkillYearsIntoCandidateSkills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "years_of_experience",
                table: "candidate_skills",
                type: "numeric(5,1)",
                precision: 5,
                scale: 1,
                nullable: true);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.tables
                        WHERE table_schema = 'public'
                          AND table_name = 'candidate_skill_details'
                    ) THEN
                        UPDATE public.candidate_skills cs
                        SET years_of_experience = csd.years_of_experience
                        FROM public.candidate_skill_details csd
                        WHERE csd.candidate_id = cs.candidate_id
                          AND csd.skill_id = cs.skill_id
                          AND cs.years_of_experience IS NULL;

                        DROP TABLE public.candidate_skill_details;
                    END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS public.candidate_skill_details
                (
                    candidate_id uuid NOT NULL,
                    skill_id uuid NOT NULL,
                    years_of_experience numeric(5,1),
                    CONSTRAINT candidate_skill_details_pkey PRIMARY KEY (candidate_id, skill_id),
                    CONSTRAINT candidate_skill_details_candidate_id_fkey FOREIGN KEY (candidate_id)
                        REFERENCES public.candidate_profiles (id) ON DELETE CASCADE,
                    CONSTRAINT candidate_skill_details_skill_id_fkey FOREIGN KEY (skill_id)
                        REFERENCES public.skills (id) ON DELETE CASCADE
                );

                INSERT INTO public.candidate_skill_details (candidate_id, skill_id, years_of_experience)
                SELECT cs.candidate_id, cs.skill_id, cs.years_of_experience
                FROM public.candidate_skills cs
                WHERE cs.years_of_experience IS NOT NULL
                ON CONFLICT (candidate_id, skill_id) DO UPDATE
                SET years_of_experience = EXCLUDED.years_of_experience;
                """);

            migrationBuilder.DropColumn(
                name: "years_of_experience",
                table: "candidate_skills");
        }
    }
}
