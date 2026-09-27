using System.Threading.Channels;
using FinanceFoodTracker.Application.FoodLogs;

namespace FinanceFoodTracker.Infrastructure.Analysis;

public sealed class FoodLogProcessingQueue : IFoodLogProcessingQueue
{
    private readonly Channel<FoodLogJob> _channel = Channel.CreateUnbounded<FoodLogJob>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public ValueTask EnqueueAsync(Guid foodLogId, bool single = false, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(new FoodLogJob(foodLogId, single), cancellationToken);

    public IAsyncEnumerable<FoodLogJob> ReadAllAsync(CancellationToken cancellationToken = default)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}
