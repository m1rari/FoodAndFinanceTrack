namespace FinanceFoodTracker.Application.Transactions;

public sealed record TransactionDto(
    Guid Id,
    Guid AccountId,
    Guid? CategoryId,
    string? CategoryName,
    string Type,
    decimal Amount,
    string Currency,
    DateTimeOffset OccurredAt,
    string Source,
    string? Comment,
    Guid? ReceiptId,
    string? ReceiptMerchantName,
    bool IsTransfer,
    DateTimeOffset CreatedAt);

public sealed record CreateTransactionRequest(
    decimal Amount,
    string Type,
    Guid? CategoryId = null,
    Guid? AccountId = null,
    DateTimeOffset? OccurredAt = null,
    string? Comment = null);

public sealed record UpdateTransactionRequest(
    decimal? Amount = null,
    Guid? CategoryId = null,
    bool ClearCategory = false,
    DateTimeOffset? OccurredAt = null,
    string? Comment = null,
    bool ClearComment = false);

public sealed record TransactionFilter(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    Guid? CategoryId = null,
    string? Type = null);
