namespace FinanceFoodTracker.Application.Statements.Analysis;

public sealed record StatementAnalysisRequest(
    string Text,
    IReadOnlyList<string> ExpenseCategories,
    IReadOnlyList<string> IncomeCategories,
    string SessionId);

public sealed record StatementOperationResult(
    DateTimeOffset OccurredAt,
    decimal Amount,
    string Direction,
    string? Description,
    string? Place,
    string? Currency,
    string? Mcc,
    bool IsTransfer,
    string? CategoryHint,
    decimal? Confidence);

public sealed record StatementAnalysisResult(
    string? Currency,
    decimal? OpeningBalance,
    decimal? ClosingBalance,
    IReadOnlyList<StatementOperationResult> Operations,
    string? RawResponse);
