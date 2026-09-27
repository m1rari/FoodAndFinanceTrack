using FinanceFoodTracker.Application.FoodLogs;

namespace FinanceFoodTracker.Application.FoodShares;

public interface IFoodShareService
{
    Task<FoodShareDto> CreateAsync(Guid userId, Guid foodLogId, CancellationToken cancellationToken = default);

    Task<FoodSharePreviewDto> GetAsync(string token, CancellationToken cancellationToken = default);

    Task<FoodLogDto> ClaimAsync(Guid userId, string token, CancellationToken cancellationToken = default);
}
