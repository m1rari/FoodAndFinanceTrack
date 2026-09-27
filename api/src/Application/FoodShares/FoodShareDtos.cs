namespace FinanceFoodTracker.Application.FoodShares;

public sealed record FoodShareDto(string Token, string? Url);

public sealed record FoodSharePreviewDto(
    string DishName,
    string? UserContext,
    decimal? CaloriesMin,
    decimal? CaloriesMax,
    decimal? ProteinG,
    decimal? FatG,
    decimal? CarbsG);
