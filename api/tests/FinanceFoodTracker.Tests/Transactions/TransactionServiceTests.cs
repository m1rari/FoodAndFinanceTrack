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

    [Fact]
    public async Task Update_ClearCategoryAndComment_ResetsThem()
    {
        var (db, userId, categoryId) = await SeedAsync();
        await using var _ = db;
        var service = new TransactionService(db);

        var created = await service.CreateAsync(
            userId,
            new CreateTransactionRequest(5m, "Expense", categoryId, Comment: "тест"));
        var updated = await service.UpdateAsync(
            userId,
            created.Id,
            new UpdateTransactionRequest(ClearCategory: true, ClearComment: true));

        Assert.Null(updated.CategoryId);
        Assert.Null(updated.CategoryName);
        Assert.Null(updated.Comment);
    }

    [Fact]
    public async Task Delete_Manual_Removes()
    {
        var (db, userId, categoryId) = await SeedAsync();
        await using var _ = db;
        var service = new TransactionService(db);

        var created = await service.CreateAsync(userId, new CreateTransactionRequest(5m, "Expense", categoryId));
        await service.DeleteAsync(userId, created.Id);

        Assert.Empty(db.Transactions.Where(t => t.Id == created.Id));
    }

    [Fact]
    public async Task Delete_ReceiptSource_Throws()
    {
        var (db, userId, _) = await SeedAsync();
        await using var _1 = db;
        var account = db.Accounts.First();
        var receiptTransaction = new Transaction
        {
            UserId = userId,
            AccountId = account.Id,
            Type = TransactionType.Expense,
            Amount = 5m,
            Currency = "BYN",
            OccurredAt = DateTimeOffset.UtcNow,
            Source = TransactionSource.Receipt
        };
        db.Transactions.Add(receiptTransaction);
        await db.SaveChangesAsync();
        var service = new TransactionService(db);

        await Assert.ThrowsAsync<ValidationException>(() => service.DeleteAsync(userId, receiptTransaction.Id));
    }

    [Fact]
    public async Task Delete_OtherUser_Throws()
    {
        var (db, userId, categoryId) = await SeedAsync();
        await using var _ = db;
        var service = new TransactionService(db);
        var created = await service.CreateAsync(userId, new CreateTransactionRequest(5m, "Expense", categoryId));

        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(Guid.NewGuid(), created.Id));
    }

    [Fact]
    public async Task Update_ChangeCategory_SetsNewCategory()
    {
        var (db, userId, categoryId) = await SeedAsync();
        await using var _ = db;
        var service = new TransactionService(db);
        var other = new Category { Name = "Транспорт", Type = TransactionType.Expense, IsSystem = true };
        db.Categories.Add(other);
        await db.SaveChangesAsync();

        var created = await service.CreateAsync(userId, new CreateTransactionRequest(5m, "Expense", categoryId));
        var updated = await service.UpdateAsync(userId, created.Id, new UpdateTransactionRequest(CategoryId: other.Id));

        Assert.Equal(other.Id, updated.CategoryId);
        Assert.Equal("Транспорт", updated.CategoryName);
    }
}
