using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RecruitPro.Application.Automation;
using RecruitPro.Application.Common;
using RecruitPro.Application.Configurations;
using RecruitPro.Application.Interfaces.IServices.Automation;
using RecruitPro.Application.Services;
using RecruitPro.Domain.Automation;
using RecruitPro.Domain.Enums;
using RecruitPro.Infrastructure.Data;

namespace RecruitPro.Infrastructure.Service.Automation;

/// <summary>
/// Time-based producer for the HeadReviewOverdue event. Finds applications still in ManagerReview whose
/// hand-off date is older than the configured threshold and publishes one windowed (per-day) event each,
/// so the same application produces at most one overdue event per 24h window (no reminder spam).
/// </summary>
public class HeadReviewOverdueScanner : IHeadReviewOverdueScanner
{
    private readonly AppDbContext _db;
    private readonly IRecruitProEventBus _eventBus;
    private readonly WorkflowAutomationSettings _settings;

    public HeadReviewOverdueScanner(AppDbContext db, IRecruitProEventBus eventBus, IOptions<WorkflowAutomationSettings> settings)
    {
        _db = db;
        _eventBus = eventBus;
        _settings = settings.Value;
    }

    public async Task<int> ScanAsync(CancellationToken cancellationToken = default)
    {
        DateTime now = DbDateTime.Now;
        DateTime cutoff = now.AddDays(-_settings.HeadReviewOverdueDays);

        List<Domain.Entities.Application> overdue = await _db.Applications.AsNoTracking()
            .Include(a => a.Job).ThenInclude(j => j.Department)
            .Include(a => a.User)
            .Where(a => a.Status == ApplicationStatus.ManagerReview
                        && a.DepartmentHeadReviewRequestedAt != null
                        && a.DepartmentHeadReviewRequestedAt <= cutoff)
            .ToListAsync(cancellationToken);

        int published = 0;
        foreach (Domain.Entities.Application application in overdue)
        {
            ApplicationOwnership ownership = ApplicationOwnershipResolver.Resolve(application);
            DateTime requestedAt = application.DepartmentHeadReviewRequestedAt!.Value;
            double overdueDays = Math.Floor((now - requestedAt).TotalDays);

            string dedupKey = WorkflowDedup.ForWindow(
                WorkflowEventTypes.HeadReviewOverdue, "app", application.Id, DbDateTime.Today);

            var payload = new
            {
                applicationId = application.Id,
                jobId = application.JobId,
                jobTitle = application.Job?.Title,
                candidateUserId = application.UserId,
                departmentId = application.Job?.DepartmentId,
                recruiterId = ownership.RecruiterUserId,
                departmentHeadId = ownership.DepartmentHeadUserId,
                status = application.Status.ToString(),
                departmentHeadReviewRequestedAt = requestedAt,
                overdueDays,
            };

            WorkflowMode mode = await _eventBus.PublishAsync(
                WorkflowEventTypes.HeadReviewOverdue, "Application", application.Id, dedupKey, payload, now);

            if (mode != WorkflowMode.Disabled)
            {
                published++;
            }
        }

        return published;
    }
}
