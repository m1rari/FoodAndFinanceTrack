namespace FinanceFoodTracker.Application.Reports;

public sealed record CategorySummaryDto(
    Guid? CategoryId,
    string? CategoryName,
    string Type,
    decimal Total);

public sealed record ReportSummaryDto(
    DateTimeOffset From,
    DateTimeOffset To,
    string Currency,
    decimal TotalIncome,
    decimal TotalExpense,
    IReadOnlyList<CategorySummaryDto> ByCategory);
