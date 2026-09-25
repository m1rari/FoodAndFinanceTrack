namespace FinanceFoodTracker.Application.FoodLogs;

public interface IFoodLogService
{
    Task<FoodLogDto> CreateAsync(Guid userId, byte[] content, string fileName, string? context = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FoodLogDto>> GetAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    Task<FoodLogDto> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<FoodLogImageDto> GetImageAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<FoodLogDto> UpdateAsync(Guid userId, Guid id, UpdateFoodLogRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<FoodLogDto> ReanalyzeAsync(Guid userId, Guid id, string? context, CancellationToken cancellationToken = default);
}
