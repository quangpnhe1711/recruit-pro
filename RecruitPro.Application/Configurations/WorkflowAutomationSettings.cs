using System.Collections.Generic;
using RecruitPro.Domain.Automation;

namespace RecruitPro.Application.Configurations;

/// <summary>
/// Per-event cutover configuration (appsettings "WorkflowAutomation"). DefaultMode is safe (Shadow)
/// unless overridden. EventModes overrides the mode for a specific event type. When the whole feature
/// is disabled, every event resolves to <see cref="WorkflowMode.Disabled"/> and no workflow runs.
/// </summary>
public class WorkflowAutomationSettings
{
    public bool Enabled { get; set; } = true;

    public string DefaultMode { get; set; } = "Shadow";

    /// <summary>Event-type -> mode ("Disabled" | "Shadow" | "Live").</summary>
    public Dictionary<string, string> EventModes { get; set; } = new();

    /// <summary>Overdue threshold for the Head Review scheduler (days). Default 3.</summary>
    public int HeadReviewOverdueDays { get; set; } = 3;

    /// <summary>Cooldown that prevents duplicate overdue reminders (hours). Default 24.</summary>
    public int HeadReviewOverdueCooldownHours { get; set; } = 24;

    /// <summary>Max execution attempts before an action is dead-lettered. Default 3.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Resolves the effective cutover mode for an event type.</summary>
    public WorkflowMode ResolveMode(string eventType)
    {
        if (!Enabled)
        {
            return WorkflowMode.Disabled;
        }

        if (EventModes.TryGetValue(eventType, out string? raw) && TryParse(raw, out WorkflowMode mode))
        {
            return mode;
        }

        return TryParse(DefaultMode, out WorkflowMode fallback) ? fallback : WorkflowMode.Shadow;
    }

    private static bool TryParse(string? value, out WorkflowMode mode)
        => System.Enum.TryParse(value, ignoreCase: true, out mode);
}
