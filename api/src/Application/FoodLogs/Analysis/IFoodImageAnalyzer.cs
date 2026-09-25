namespace FinanceFoodTracker.Application.FoodLogs.Analysis;

public interface IFoodImageAnalyzer
{
    Task<FoodAnalysisResult> AnalyzeAsync(FoodAnalysisRequest request, CancellationToken cancellationToken = default);
}
