namespace FinanceFoodTracker.Application.SavedDishes;

public sealed record SavedDishDto(
    Guid Id,
    string Name,
    decimal? CaloriesMin,
    decimal? CaloriesMax,
    decimal? ProteinMinG,
    decimal? ProteinMaxG,
    decimal? FatMinG,
    decimal? FatMaxG,
    decimal? CarbsMinG,
    decimal? CarbsMaxG,
    bool IsFavorite,
    int UseCount,
    DateTimeOffset LastUsedAt);

public sealed record SetFavoriteRequest(bool IsFavorite);
