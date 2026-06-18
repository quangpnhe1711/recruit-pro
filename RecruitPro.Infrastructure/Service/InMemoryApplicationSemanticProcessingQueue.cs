using System.Threading.Channels;
using RecruitPro.Application.Interfaces;

namespace RecruitPro.Infrastructure.Service;

public class InMemoryApplicationSemanticProcessingQueue : IApplicationSemanticProcessingQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public ValueTask EnqueueAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(applicationId, cancellationToken);
    }

    public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}
