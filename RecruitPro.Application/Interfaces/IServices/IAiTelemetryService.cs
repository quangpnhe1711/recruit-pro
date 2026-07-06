using RecruitPro.Application.Common;

namespace RecruitPro.Application.Interfaces.IServices;

/// <summary>
/// v5.1 — records one AI invocation. Fire-and-forget from the caller's perspective: the implementation
/// enqueues to a bounded write-behind channel and returns immediately. It never throws and never blocks
/// the request; a full queue drops the record (logged), and DB writes happen on a background worker.
/// </summary>
public interface IAiTelemetryService
{
    ValueTask RecordAsync(AiTelemetryRecord record, CancellationToken cancellationToken = default);
}
