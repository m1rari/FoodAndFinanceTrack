using FinanceFoodTracker.Application.Reports;
using FinanceFoodTracker.Domain.Entities;
using FinanceFoodTracker.Domain.Enums;
using Xunit;

namespace FinanceFoodTracker.Tests.Reports;

public sealed class ReportServiceTests
{
    [Fact]
    public async Task GetSummary_AggregatesIncomeExpenseAndCategories()
    {
        await using var db = TestDb.Create();
        var user = new User { TelegramId = 1 };
        var account = new Account { UserId = user.Id };
        var food = new Category { Name = "Продукты", Type = TransactionType.Expense, IsSystem = true };
        var salary = new Category { Name = "Зарплата", Type = TransactionType.Income, IsSystem = true };

        db.Users.Add(user);
        db.Accounts.Add(account);
        db.Categories.AddRange(food, salary);

        var now = DateTimeOffset.UtcNow;
        db.Transactions.AddRange(
            new Transaction { UserId = user.Id, AccountId = account.Id, CategoryId = food.Id, Type = TransactionType.Expense, Amount = 30m, OccurredAt = now },
            new Transaction { UserId = user.Id, AccountId = account.Id, CategoryId = food.Id, Type = TransactionType.Expense, Amount = 20m, OccurredAt = now },
            new Transaction { UserId = user.Id, AccountId = account.Id, CategoryId = salary.Id, Type = TransactionType.Income, Amount = 1000m, OccurredAt = now });
        await db.SaveChangesAsync();

        var service = new ReportService(db);
        var summary = await service.GetSummaryAsync(user.Id, now.AddDays(-1), now.AddDays(1));

        Assert.Equal(1000m, summary.TotalIncome);
        Assert.Equal(50m, summary.TotalExpense);
        Assert.Equal(2, summary.ByCategory.Count);

        var foodSummary = summary.ByCategory.Single(c => c.CategoryId == food.Id);
        Assert.Equal(50m, foodSummary.Total);
    }
}
