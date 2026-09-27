namespace FinanceFoodTracker.Application.FoodLogs.Analysis;

public sealed record FoodAnalysisRequest(
    string ImagePath,
    string SessionId,
    string? Context = null,
    decimal? PortionGrams = null,
    bool SingleItem = false);

public sealed record FoodAnalysisItemResult(
    string? DishName,
    decimal? PortionGrams,
    decimal? CaloriesMin,
    decimal? CaloriesMax,
    decimal? ProteinMinG,
    decimal? ProteinMaxG,
    decimal? FatMinG,
    decimal? FatMaxG,
    decimal? CarbsMinG,
    decimal? CarbsMaxG,
    decimal? Confidence);

public sealed record FoodAnalysisResult(
    IReadOnlyList<FoodAnalysisItemResult> Items,
    string? RawText = null,
    string? RawResponse = null);
