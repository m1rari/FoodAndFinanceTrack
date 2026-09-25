namespace FinanceFoodTracker.Application.FoodLogs.Analysis;

public sealed record FoodAnalysisRequest(string ImagePath, string SessionId, string? Context = null);

public sealed record FoodAnalysisResult(
    string? DishName = null,
    decimal? CaloriesMin = null,
    decimal? CaloriesMax = null,
    decimal? ProteinMinG = null,
    decimal? ProteinMaxG = null,
    decimal? FatMinG = null,
    decimal? FatMaxG = null,
    decimal? CarbsMinG = null,
    decimal? CarbsMaxG = null,
    decimal? Confidence = null,
    string? RawText = null,
    string? RawResponse = null);
