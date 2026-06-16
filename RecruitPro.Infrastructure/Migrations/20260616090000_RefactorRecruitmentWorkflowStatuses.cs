using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruitPro.Infrastructure.Migrations
{
    public partial class RefactorRecruitmentWorkflowStatuses : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE applications
SET status = CASE
    WHEN status = 'Pending' THEN 'Applied'
    WHEN status = 'Reviewing' THEN 'Screening'
    WHEN status = 'HrScreening' THEN 'Screening'
    WHEN status = 'Interviewing' THEN 'Interview'
    WHEN status = 'InterviewScheduled' THEN 'Interview'
    WHEN status = 'ManagerReview' AND EXISTS (
        SELECT 1
        FROM application_offers ao
        WHERE ao.application_id = applications.id
          AND ao.status = 'Sent'
    ) THEN 'Offer'
    WHEN status = 'ManagerReview' AND EXISTS (
        SELECT 1
        FROM application_offers ao
        WHERE ao.application_id = applications.id
    ) THEN 'Offer'
    WHEN status = 'WaitingOffer' THEN 'Offer'
    WHEN status = 'OfferSent' THEN 'Offer'
    WHEN status = 'Offered' THEN 'Offer'
    WHEN status = 'ManagerReview' THEN 'ManagerReview'
    WHEN status = 'Accepted' THEN 'Hired'
    WHEN status = 'Rejected' THEN 'Rejected'
    ELSE status
END;
");

            migrationBuilder.Sql(@"
ALTER TABLE applications
ALTER COLUMN status SET DEFAULT 'Applied';
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE applications
SET status = CASE
    WHEN status = 'Applied' THEN 'Pending'
    WHEN status = 'Screening' THEN 'Reviewing'
    WHEN status = 'Interview' THEN 'Interviewing'
    WHEN status = 'Offer' THEN 'ManagerReview'
    WHEN status = 'Hired' THEN 'Accepted'
    WHEN status = 'OfferDeclined' THEN 'Rejected'
    ELSE status
END;
");

            migrationBuilder.Sql(@"
ALTER TABLE applications
ALTER COLUMN status SET DEFAULT 'Pending';
");
        }
    }
}
