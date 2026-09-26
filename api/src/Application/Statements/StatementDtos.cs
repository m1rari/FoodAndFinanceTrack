namespace FinanceFoodTracker.Application.Statements;

public sealed record StatementOperationDto(
    DateTimeOffset OccurredAt,
    decimal Amount,
    string Direction,
    string? Description,
    string? Place,
    string? Currency,
    string? Mcc,
    bool IsTransfer,
    Guid? CategoryId,
    string? CategoryName,
    string? CategoryHint,
    decimal? Confidence,
    Guid? LinkTransactionId = null);

public sealed record StatementMatchDto(int Index, IReadOnlyList<Transactions.TransactionDto> Candidates);

public sealed record StatementDto(
    Guid Id,
    string FileName,
    string Status,
    bool Confirmed,
    int CreatedCount,
    DateTimeOffset CreatedAt,
    string? Error,
    IReadOnlyList<StatementOperationDto> Operations);

public sealed record ConfirmStatementRequest(IReadOnlyList<StatementOperationDto> Operations);
