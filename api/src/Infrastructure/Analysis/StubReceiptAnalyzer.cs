using FinanceFoodTracker.Application.Receipts.Analysis;
using Microsoft.Extensions.Logging;

namespace FinanceFoodTracker.Infrastructure.Analysis;

public sealed class StubReceiptAnalyzer : IReceiptAnalyzer
{
    private readonly ILogger<StubReceiptAnalyzer> _logger;

    public StubReceiptAnalyzer(ILogger<StubReceiptAnalyzer> logger)
    {
        _logger = logger;
    }

    public Task<ReceiptAnalysisResult> AnalyzeAsync(ReceiptAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "AI-провайдер не настроен: чек {ImagePath} требует ручного разбора.",
            request.ImagePath);

        return Task.FromResult(new ReceiptAnalysisResult());
    }
}
