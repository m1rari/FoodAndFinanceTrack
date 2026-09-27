namespace FinanceFoodTracker.Application.FoodLogs;

public interface IFoodLogProcessor
{
    Task ProcessAsync(Guid foodLogId, bool single = false, CancellationToken cancellationToken = default);
}
