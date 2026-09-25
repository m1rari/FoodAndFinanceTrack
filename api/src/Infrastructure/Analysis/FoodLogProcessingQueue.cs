using System.Threading.Channels;
using FinanceFoodTracker.Application.FoodLogs;

namespace FinanceFoodTracker.Infrastructure.Analysis;

public sealed class FoodLogProcessingQueue : IFoodLogProcessingQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public ValueTask EnqueueAsync(Guid foodLogId, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(foodLogId, cancellationToken);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken = default)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}
