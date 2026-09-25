namespace FinanceFoodTracker.Application.Receipts;

public sealed record ReceiptItemDto(
    Guid Id,
    string Name,
    decimal Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    Guid? CategoryId,
    string? CategoryName,
    decimal? Confidence);

public sealed record ReceiptDto(
    Guid Id,
    string? MerchantName,
    DateTimeOffset? PurchaseDate,
    decimal? TotalAmount,
    string Status,
    decimal? Confidence,
    DateTimeOffset CreatedAt,
    string ImageUrl,
    IReadOnlyList<ReceiptItemDto> Items,
    bool Confirmed);

public sealed record ReceiptImageDto(byte[] Content, string ContentType);

public sealed record ReceiptSummaryDto(
    Guid Id,
    string? MerchantName,
    DateTimeOffset? PurchaseDate,
    decimal? TotalAmount,
    string Status,
    int ItemCount,
    bool Confirmed,
    DateTimeOffset CreatedAt);

public sealed record CreateReceiptItemRequest(
    string Name,
    decimal Quantity,
    decimal UnitPrice,
    decimal? TotalPrice = null,
    Guid? CategoryId = null);

public sealed record UpdateReceiptItemRequest(
    string? Name = null,
    decimal? Quantity = null,
    decimal? UnitPrice = null,
    decimal? TotalPrice = null,
    Guid? CategoryId = null,
    bool ClearCategory = false);
