using FinanceFoodTracker.Application.FoodLogs;
using FinanceFoodTracker.Domain.Entities;

namespace FinanceFoodTracker.Application.SavedDishes;

public interface ISavedDishService
{
    Task<IReadOnlyList<SavedDishDto>> GetAsync(Guid userId, bool? favorite, int limit, CancellationToken cancellationToken = default);

    Task<FoodLogDto> AddToDiaryAsync(Guid userId, Guid savedDishId, CancellationToken cancellationToken = default);

    Task<SavedDishDto> SetFavoriteAsync(Guid userId, Guid savedDishId, bool isFavorite, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid userId, Guid savedDishId, CancellationToken cancellationToken = default);

    Task<SavedDishDto?> UpsertFromFoodLogAsync(FoodLog log, bool? favorite, CancellationToken cancellationToken = default);

    Task SetFavoriteFromFoodLogAsync(Guid userId, Guid foodLogId, bool isFavorite, CancellationToken cancellationToken = default);
}
