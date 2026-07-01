using RecruitPro.Domain.Automation;

namespace RecruitPro.Application.Services.Automation;

/// <summary>
/// Pure decision tree that turns raw diagnostic counts into a single human Vietnamese reason a workflow
/// produced no execution. Kept free of DB/EF so it is unit-testable and is the one place the "why nothing
/// ran" logic lives. Returns null when the workflow is running normally.
/// </summary>
public static class AutomationDiagnosticsReasoner
{
    public static string? ResolveNoExecutionReason(
        bool automationEnabled,
        bool workflowEnabled,
        bool hasActiveVersion,
        WorkflowMode effectiveMode,
        int eventsTodayOfType,
        int executionsToday,
        int pendingEventsOfType,
        int skippedCount,
        int successCount)
    {
        if (!automationEnabled)
            return "Tự động hóa đang bị tắt toàn hệ thống.";
        if (!workflowEnabled)
            return "Workflow đang bị tắt.";
        if (!hasActiveVersion)
            return "Workflow chưa có phiên bản active (chưa xuất bản).";
        if (effectiveMode == WorkflowMode.Disabled)
            return "Sự kiện này đang ở chế độ Disabled.";
        if (eventsTodayOfType == 0)
            return "Chưa có sự kiện loại này hôm nay — hãy thực hiện thao tác nghiệp vụ tương ứng.";
        if (executionsToday == 0 && pendingEventsOfType > 0)
            return "Có sự kiện nhưng worker chưa xử lý (kiểm tra worker heartbeat).";
        if (skippedCount > 0 && successCount == 0)
            return "Điều kiện workflow không thỏa (ví dụ chưa có Trưởng bộ phận phụ trách).";
        return null;
    }
}
