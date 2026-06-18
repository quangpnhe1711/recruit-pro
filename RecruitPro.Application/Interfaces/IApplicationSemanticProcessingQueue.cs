namespace RecruitPro.Application.Interfaces;

public interface IApplicationSemanticProcessingQueue
{
    ValueTask EnqueueAsync(Guid applicationId, CancellationToken cancellationToken = default);
    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken);
}
