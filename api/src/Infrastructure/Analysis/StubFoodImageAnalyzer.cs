using FinanceFoodTracker.Application.FoodLogs.Analysis;
using Microsoft.Extensions.Logging;

namespace FinanceFoodTracker.Infrastructure.Analysis;

public sealed class StubFoodImageAnalyzer : IFoodImageAnalyzer
{
    private readonly ILogger<StubFoodImageAnalyzer> _logger;

    public StubFoodImageAnalyzer(ILogger<StubFoodImageAnalyzer> logger)
    {
        _logger = logger;
    }

    public Task<FoodAnalysisResult> AnalyzeAsync(FoodAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "AI-провайдер не настроен: блюдо {ImagePath} требует ручной оценки.",
            request.ImagePath);

        return Task.FromResult(new FoodAnalysisResult());
    }
}
