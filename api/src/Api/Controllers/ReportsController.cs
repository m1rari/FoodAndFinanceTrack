using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Reports;
using Microsoft.AspNetCore.Mvc;

namespace FinanceFoodTracker.Api.Controllers;

[ApiController]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportService _reports;
    private readonly ICurrentUser _currentUser;

    public ReportsController(IReportService reports, ICurrentUser currentUser)
    {
        _reports = reports;
        _currentUser = currentUser;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ReportSummaryDto>> Summary(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var periodStart = from ?? new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var periodEnd = to ?? periodStart.AddMonths(1).AddTicks(-1);

        return Ok(await _reports.GetSummaryAsync(_currentUser.UserId, periodStart, periodEnd, cancellationToken));
    }
}
