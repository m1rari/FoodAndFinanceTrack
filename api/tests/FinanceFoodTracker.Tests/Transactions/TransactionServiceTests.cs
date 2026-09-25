using FinanceFoodTracker.Application.Common.Exceptions;
using FinanceFoodTracker.Application.Transactions;
using FinanceFoodTracker.Domain.Entities;
using FinanceFoodTracker.Domain.Enums;
using Xunit;

namespace FinanceFoodTracker.Tests.Transactions;

public sealed class TransactionServiceTests
{
    private static async Task<(Infrastructure.Persistence.AppDbContext Db, Guid UserId, Guid CategoryId)> SeedAsync()
    {
        var db = TestDb.Create();
        var user = new User { TelegramId = 555, Username = "bob" };
        var account = new Account { UserId = user.Id };
        var category = new Category { Name = "Продукты", Type = TransactionType.Expense, IsSystem = true };

        db.Users.Add(user);
        db.Accounts.Add(account);
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        return (db, user.Id, category.Id);
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsTransaction()
    {
        var (db, userId, categoryId) = await SeedAsync();
        await using var _ = db;
        var service = new TransactionService(db);

        var result = await service.CreateAsync(userId, new CreateTransactionRequest(12.50m, "Expense", categoryId));

        Assert.Equal(12.50m, result.Amount);
        Assert.Equal("Expense", result.Type);
        Assert.Equal("Продукты", result.CategoryName);
        Assert.Equal("BYN", result.Currency);
    }

    [Fact]
    public async Task Create_NonPositiveAmount_Throws()
    {
        var (db, userId, _) = await SeedAsync();
        await using var _1 = db;
        var service = new TransactionService(db);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(userId, new CreateTransactionRequest(0m, "Expense")));
    }

    [Fact]
    public async Task Create_CategoryTypeMismatch_Throws()
    {
        var (db, userId, categoryId) = await SeedAsync();
        await using var _ = db;
        var service = new TransactionService(db);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(userId, new CreateTransactionRequest(10m, "Income", categoryId)));
    }

    [Fact]
    public async Task Update_Amount_ChangesValue()
    {
        var (db, userId, categoryId) = await SeedAsync();
        await using var _ = db;
        var service = new TransactionService(db);

        var created = await service.CreateAsync(userId, new CreateTransactionRequest(5m, "Expense", categoryId));
        var updated = await service.UpdateAsync(userId, created.Id, new UpdateTransactionRequest(Amount: 42m));

        Assert.Equal(42m, updated.Amount);
    }
}
