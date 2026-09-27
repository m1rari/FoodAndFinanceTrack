namespace FinanceFoodTracker.Application.FoodLogs;

public sealed record FoodLogJob(Guid FoodLogId, bool Single);

public interface IFoodLogProcessingQueue
{
    ValueTask EnqueueAsync(Guid foodLogId, bool single = false, CancellationToken cancellationToken = default);

    IAsyncEnumerable<FoodLogJob> ReadAllAsync(CancellationToken cancellationToken = default);
}
