namespace FinanceFoodTracker.Application.FoodLogs;

public interface IFoodLogProcessingQueue
{
    ValueTask EnqueueAsync(Guid foodLogId, CancellationToken cancellationToken = default);

    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken = default);
}
