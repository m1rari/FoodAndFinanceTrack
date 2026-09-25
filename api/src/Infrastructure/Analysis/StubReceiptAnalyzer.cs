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

    public Task<ReceiptAnalysisResult> AnalyzeAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Заглушка IReceiptAnalyzer: файл {ImagePath} требует ручного разбора.", imagePath);
        return Task.FromResult(new ReceiptAnalysisResult());
    }
}
