namespace FinanceFoodTracker.Application.Reports;

public interface IReportService
{
    Task<ReportSummaryDto> GetSummaryAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}
