using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RecruitPro.Application.Common;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices.Automation;
using RecruitPro.Domain.Automation;

namespace RecruitPro.Application.Services.Automation;

/// <summary>
/// Seeds the four default workflow templates as published+active definitions. Idempotent by name, so it
/// is safe to run on every startup. All seed to Shadow (safe): a deployment flips them Live centrally via
/// the per-event cutover config, so no template ever sends a duplicate notification on first run.
/// </summary>
public class WorkflowTemplateSeeder : IWorkflowTemplateSeeder
{
    private readonly IWorkflowRepository _workflows;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<WorkflowTemplateSeeder> _logger;

    public WorkflowTemplateSeeder(IWorkflowRepository workflows, IUnitOfWork unitOfWork, ILogger<WorkflowTemplateSeeder> logger)
    {
        _workflows = workflows;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedOneAsync(
            "Pass CV → Notify Head Review",
            "Khi HR chuyển hồ sơ sang bước Trưởng bộ phận duyệt, thông báo cho Trưởng bộ phận phụ trách.",
            WorkflowEventTypes.PassedToHeadReview,
            conditions: "[{\"field\":\"departmentHeadId\",\"operator\":\"exists\",\"value\":null}]",
            actions: "[{\"type\":\"notify_user\",\"config\":{\"recipients\":[\"assignedDepartmentHead\"],\"eventCode\":\"workflow_head_review\",\"title\":\"Hồ sơ chờ bạn duyệt\",\"body\":\"Có hồ sơ mới cần Trưởng bộ phận xem xét.\"}}]");

        await SeedOneAsync(
            "Head Review Overdue Reminder",
            "Nhắc Trưởng bộ phận khi hồ sơ ở bước duyệt quá hạn (mặc định 3 ngày), có cooldown 24 giờ.",
            WorkflowEventTypes.HeadReviewOverdue,
            conditions: "[{\"field\":\"overdueDays\",\"operator\":\"greater_than_or_equal\",\"value\":\"3\"}]",
            actions: "[{\"type\":\"send_reminder\",\"config\":{\"recipients\":[\"assignedDepartmentHead\"],\"cooldownHours\":24,\"eventCode\":\"workflow_head_review_overdue\",\"title\":\"Nhắc duyệt hồ sơ\",\"body\":\"Có hồ sơ đang chờ bạn duyệt quá hạn.\"}}]");

        await SeedOneAsync(
            "High-fit Candidate Alert",
            "Khi ứng viên nộp hồ sơ và điểm phù hợp cao (>= 80), báo cho HR/recruiter phụ trách.",
            WorkflowEventTypes.CandidateApplied,
            conditions: "[{\"field\":\"finalScore\",\"operator\":\"greater_than_or_equal\",\"value\":\"80\"}]",
            actions: "[{\"type\":\"notify_user\",\"config\":{\"recipients\":[\"assignedRecruiter\"],\"eventCode\":\"workflow_high_fit\",\"title\":\"Ứng viên tiềm năng\",\"body\":\"Có ứng viên điểm phù hợp cao vừa ứng tuyển.\"}}]");

        await SeedOneAsync(
            "Interview Completed Follow-up",
            "Khi phỏng vấn hoàn tất, gửi gợi ý bước tiếp theo (theo luật, không cần AI) cho HR/Manager.",
            WorkflowEventTypes.InterviewCompleted,
            conditions: "[]",
            actions: "[{\"type\":\"rule_based_next_step_suggestion\",\"config\":{\"scoreField\":\"finalScore\",\"recipients\":[\"assignedRecruiter\",\"assignedDepartmentHead\"],\"eventCode\":\"workflow_interview_followup\",\"title\":\"Gợi ý sau phỏng vấn\"}}]");
    }

    private async Task SeedOneAsync(string name, string description, string eventType, string conditions, string actions)
    {
        if (await _workflows.DefinitionExistsByNameAsync(name))
        {
            return;
        }

        Guid defId = Guid.NewGuid();
        WorkflowDefinition definition = new()
        {
            Id = defId,
            Name = name,
            Description = description,
            IsEnabled = true,
            CreatedAt = DbDateTime.Now,
        };
        WorkflowDefinitionVersion version = new()
        {
            Id = Guid.NewGuid(),
            WorkflowDefinitionId = defId,
            VersionNo = 1,
            TriggerJson = $"{{\"eventType\":\"{eventType}\"}}",
            ConditionsJson = conditions,
            ActionsJson = actions,
            Mode = WorkflowMode.Shadow,
            IsActive = true,
            PublishedAt = DbDateTime.Now,
            CreatedAt = DbDateTime.Now,
        };

        // Two saves: the active_version_id FK points at the version, so inserting both in one batch with
        // the FK already set is a circular dependency. Persist the rows first, then link the active version.
        await _workflows.AddDefinitionAsync(definition);
        await _workflows.AddVersionAsync(version);
        await _unitOfWork.SaveChangesAsync();

        definition.ActiveVersionId = version.Id;
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Seeded workflow template '{Name}'", name);
    }
}
