namespace FinanceFoodTracker.Application.Receipts.Analysis;

public interface IReceiptAnalyzer
{
    Task<ReceiptAnalysisResult> AnalyzeAsync(ReceiptAnalysisRequest request, CancellationToken cancellationToken = default);
}
