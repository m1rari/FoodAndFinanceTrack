using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceFoodTracker.Application.Reports;

public sealed class ReportService : IReportService
{
    private readonly IApplicationDbContext _db;

    public ReportService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ReportSummaryDto> GetSummaryAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var fromUtc = from.ToUniversalTime();
        var toUtc = to.ToUniversalTime();

        var transactions = await _db.Transactions
            .Where(t => t.UserId == userId && !t.IsTransfer && t.OccurredAt >= fromUtc && t.OccurredAt <= toUtc)
            .Include(t => t.Category)
            .ToListAsync(cancellationToken);

        var totalIncome = transactions
            .Where(t => t.Type == TransactionType.Income)
            .Sum(t => t.Amount);

        var totalExpense = transactions
            .Where(t => t.Type == TransactionType.Expense)
            .Sum(t => t.Amount);

        var byCategory = transactions
            .GroupBy(t => new { t.CategoryId, CategoryName = t.Category != null ? t.Category.Name : null, t.Type })
            .Select(g => new CategorySummaryDto(g.Key.CategoryId, g.Key.CategoryName, g.Key.Type.ToString(), g.Sum(t => t.Amount)))
            .OrderByDescending(c => c.Total)
            .ToList();

        var currency = transactions.FirstOrDefault()?.Currency ?? "BYN";

        return new ReportSummaryDto(from, to, currency, totalIncome, totalExpense, byCategory);
    }
}
