using System;

namespace RecruitPro.Application.Automation;

/// <summary>Pure, unit-testable cooldown check for the send_reminder action.</summary>
public static class ReminderCooldown
{
    /// <summary>
    /// True when a reminder sent at <paramref name="lastSentAt"/> is still inside the cooldown window
    /// (so a new reminder must be suppressed). Null last-sent means "never sent" — not in cooldown.
    /// </summary>
    public static bool IsWithinCooldown(DateTime? lastSentAt, int cooldownHours, DateTime now)
    {
        if (lastSentAt is null || cooldownHours <= 0)
        {
            return false;
        }

        return now < lastSentAt.Value.AddHours(cooldownHours);
    }
}
