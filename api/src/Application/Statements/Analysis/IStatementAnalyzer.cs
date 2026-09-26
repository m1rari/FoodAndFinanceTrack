namespace FinanceFoodTracker.Application.Statements.Analysis;

public interface IStatementAnalyzer
{
    Task<StatementAnalysisResult> AnalyzeAsync(StatementAnalysisRequest request, CancellationToken cancellationToken = default);
}
