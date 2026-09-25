namespace FinanceFoodTracker.Application.Receipts.Analysis;

public sealed record ReceiptAnalysisRequest(
    string ImagePath,
    IReadOnlyList<string> Categories);

public sealed record ReceiptAnalysisItem(
    string Name,
    decimal Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    string? CategoryHint = null,
    decimal? Confidence = null);

public sealed record ReceiptAnalysisResult(
    string? MerchantName = null,
    DateTimeOffset? PurchaseDate = null,
    decimal? TotalAmount = null,
    string? RawOcrText = null,
    string? RawResponse = null,
    decimal? Confidence = null,
    IReadOnlyList<ReceiptAnalysisItem>? Items = null)
{
    public IReadOnlyList<ReceiptAnalysisItem> SafeItems => Items ?? Array.Empty<ReceiptAnalysisItem>();
}
