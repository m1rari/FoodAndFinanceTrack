namespace FinanceFoodTracker.Application.Receipts.Analysis;

public interface IReceiptAnalyzer
{
    Task<ReceiptAnalysisResult> AnalyzeAsync(string imagePath, CancellationToken cancellationToken = default);
}
