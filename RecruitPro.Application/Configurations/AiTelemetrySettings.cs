namespace RecruitPro.Application.Configurations;

/// <summary>
/// v5.1 — configuration for AI telemetry capture. Bound from the "AiTelemetry" config section.
/// Telemetry is best-effort and write-behind: it must never slow down or fail the user flow that
/// triggered the AI call.
/// </summary>
public class AiTelemetrySettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// When the bounded queue is full: true = drop the NEWEST record (never block the request);
    /// false = also drop, but the semantics are documented as "prefer keeping older records".
    /// Either way the request thread is never blocked and the drop is logged.
    /// </summary>
    public bool DropWhenQueueFull { get; set; } = true;

    public int MaxQueueSize { get; set; } = 5000;
    public int BatchSize { get; set; } = 100;
    public int FlushIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Per-model pricing used to ESTIMATE cost from token counts. Cost is always marked estimated
    /// (is_cost_estimated=true) because the provider never bills a per-call amount back to us.
    /// Key = model name. Absent/zero pricing => cost is left null.
    /// </summary>
    public Dictionary<string, AiModelPricing> Pricing { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class AiModelPricing
{
    public decimal InputPerMillionTokensUsd { get; set; }
    public decimal OutputPerMillionTokensUsd { get; set; }
}
