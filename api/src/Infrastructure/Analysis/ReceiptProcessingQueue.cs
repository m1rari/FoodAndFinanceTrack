using System.Threading.Channels;
using FinanceFoodTracker.Application.Receipts;

namespace FinanceFoodTracker.Infrastructure.Analysis;

public sealed class ReceiptProcessingQueue : IReceiptProcessingQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public ValueTask EnqueueAsync(Guid receiptId, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(receiptId, cancellationToken);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken = default)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}
