namespace FinanceFoodTracker.Application.FoodLogs;

public interface IFoodLogProcessor
{
    Task ProcessAsync(Guid foodLogId, CancellationToken cancellationToken = default);
}
