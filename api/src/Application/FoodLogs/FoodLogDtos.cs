namespace FinanceFoodTracker.Application.FoodLogs;

public sealed record FoodLogDto(
    Guid Id,
    string? DishName,
    string? UserContext,
    decimal? CaloriesMin,
    decimal? CaloriesMax,
    decimal? ProteinMinG,
    decimal? ProteinMaxG,
    decimal? FatMinG,
    decimal? FatMaxG,
    decimal? CarbsMinG,
    decimal? CarbsMaxG,
    decimal? ProteinG,
    decimal? FatG,
    decimal? CarbsG,
    string Status,
    DateTimeOffset EatenAt,
    string ImageUrl,
    DateTimeOffset CreatedAt);

public sealed record FoodLogImageDto(byte[] Content, string ContentType);

public sealed record UpdateFoodLogRequest(
    string? DishName = null,
    string? UserContext = null,
    decimal? CaloriesMin = null,
    decimal? CaloriesMax = null,
    decimal? ProteinMinG = null,
    decimal? ProteinMaxG = null,
    decimal? FatMinG = null,
    decimal? FatMaxG = null,
    decimal? CarbsMinG = null,
    decimal? CarbsMaxG = null,
    DateTimeOffset? EatenAt = null);

public sealed record ReanalyzeFoodLogRequest(string? Context = null);

public sealed record CreateFoodLogTextRequest(string Context);
