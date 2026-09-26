using FinanceFoodTracker.Application.Statements.Analysis;
using Microsoft.Extensions.Logging;

namespace FinanceFoodTracker.Infrastructure.Analysis;

public sealed class StubStatementAnalyzer : IStatementAnalyzer
{
    private readonly ILogger<StubStatementAnalyzer> _logger;

    public StubStatementAnalyzer(ILogger<StubStatementAnalyzer> logger)
    {
        _logger = logger;
    }

    public Task<StatementAnalysisResult> AnalyzeAsync(StatementAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("AI-провайдер не настроен: выписка требует ручного разбора.");
        return Task.FromResult(new StatementAnalysisResult(null, null, null, Array.Empty<StatementOperationResult>(), null));
    }
}
