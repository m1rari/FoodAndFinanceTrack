using FinanceFoodTracker.Application.Common.Exceptions;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Receipts;
using FinanceFoodTracker.Domain.Entities;
using FinanceFoodTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceFoodTracker.Tests.Receipts;

public sealed class ReceiptServiceTests
{
    private static readonly byte[] JpegBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };

    internal sealed class FakeFileStorage : IFileStorage
    {
        private readonly Dictionary<string, byte[]> _files = new();

        public List<string> Deleted { get; } = new();

        public Task<string> SaveAsync(byte[] content, string extension, CancellationToken cancellationToken = default)
        {
            var path = $"{Guid.NewGuid():N}{extension}";
            _files[path] = content;
            return Task.FromResult(path);
        }

        public Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken = default)
            => Task.FromResult(_files[path]);

        public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
        {
            Deleted.Add(path);
            _files.Remove(path);
            return Task.CompletedTask;
        }

        public bool Exists(string path) => _files.ContainsKey(path);
    }

    private sealed class FakeQueue : IReceiptProcessingQueue
    {
        public List<Guid> Enqueued { get; } = new();

        public ValueTask EnqueueAsync(Guid receiptId, CancellationToken cancellationToken = default)
        {
            Enqueued.Add(receiptId);
            return ValueTask.CompletedTask;
        }

        public async IAsyncEnumerable<Guid> ReadAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var id in Enqueued)
            {
                yield return id;
            }

            await Task.CompletedTask;
        }
    }

    private static async Task<(Infrastructure.Persistence.AppDbContext Db, Guid UserId, Guid ExpenseCategoryId, Guid IncomeCategoryId)> SeedAsync()
    {
        var db = TestDb.Create();
        var user = new User { TelegramId = 777, Username = "receipt-user" };
        var account = new Account { UserId = user.Id, Name = "Основной", Currency = "BYN" };
        var expense = new Category { Name = "Продукты", Type = TransactionType.Expense, IsSystem = true };
        var income = new Category { Name = "Зарплата", Type = TransactionType.Income, IsSystem = true };

        db.Users.Add(user);
        db.Accounts.Add(account);
        db.Categories.AddRange(expense, income);
        await db.SaveChangesAsync();

        return (db, user.Id, expense.Id, income.Id);
    }

    [Fact]
    public async Task Create_ValidJpeg_StoresImageAndEnqueues()
    {
        var (db, userId, _, _) = await SeedAsync();
        await using var _1 = db;
        var storage = new FakeFileStorage();
        var queue = new FakeQueue();
        var service = new ReceiptService(db, storage, queue);

        var result = await service.CreateAsync(userId, JpegBytes, "receipt.jpg");

        Assert.Equal("Pending", result.Status);
        Assert.False(result.Confirmed);
        Assert.Contains(result.Id, queue.Enqueued);

        var receipt = await db.Receipts.FindAsync(result.Id);
        Assert.NotNull(receipt);
        Assert.True(storage.Exists(receipt!.ImagePath));
    }

    [Fact]
    public async Task Create_UnsupportedContent_Throws()
    {
        var (db, userId, _, _) = await SeedAsync();
        await using var _1 = db;
        var service = new ReceiptService(db, new FakeFileStorage(), new FakeQueue());

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(userId, new byte[] { 0x25, 0x50, 0x44, 0x46 }, "file.pdf"));
    }

    [Fact]
    public async Task AddItem_UpdatesTotalAndStatus()
    {
        var (db, userId, expenseId, _) = await SeedAsync();
        await using var _1 = db;
        var service = new ReceiptService(db, new FakeFileStorage(), new FakeQueue());

        var created = await service.CreateAsync(userId, JpegBytes, "receipt.jpg");
        var updated = await service.AddItemAsync(userId, created.Id, new CreateReceiptItemRequest("Хлеб", 1, 1.25m, CategoryId: expenseId));

        Assert.Equal("Processed", updated.Status);
        Assert.Equal(1.25m, updated.TotalAmount);
        Assert.Single(updated.Items);
        Assert.Equal("Продукты", updated.Items[0].CategoryName);
    }

    [Fact]
    public async Task UpdateItem_ChangesFieldsAndRecomputesTotal()
    {
        var (db, userId, expenseId, _) = await SeedAsync();
        await using var _1 = db;
        var service = new ReceiptService(db, new FakeFileStorage(), new FakeQueue());

        var created = await service.CreateAsync(userId, JpegBytes, "receipt.jpg");
        var withItem = await service.AddItemAsync(userId, created.Id, new CreateReceiptItemRequest("Хлеб", 1, 1.25m));
        var itemId = withItem.Items[0].Id;

        var updated = await service.UpdateItemAsync(userId, created.Id, itemId, new UpdateReceiptItemRequest(Quantity: 3, UnitPrice: 2m, CategoryId: expenseId));

        Assert.Equal(6m, updated.TotalAmount);
        Assert.Equal(3m, updated.Items[0].Quantity);
        Assert.Equal(expenseId, updated.Items[0].CategoryId);
    }

    [Fact]
    public async Task UpdateItem_IncomeCategory_Throws()
    {
        var (db, userId, _, incomeId) = await SeedAsync();
        await using var _1 = db;
        var service = new ReceiptService(db, new FakeFileStorage(), new FakeQueue());

        var created = await service.CreateAsync(userId, JpegBytes, "receipt.jpg");
        var withItem = await service.AddItemAsync(userId, created.Id, new CreateReceiptItemRequest("Хлеб", 1, 1.25m));
        var itemId = withItem.Items[0].Id;

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateItemAsync(userId, created.Id, itemId, new UpdateReceiptItemRequest(CategoryId: incomeId)));
    }

    [Fact]
    public async Task Confirm_CreatesReceiptTransactions()
    {
        var (db, userId, expenseId, _) = await SeedAsync();
        await using var _1 = db;
        var service = new ReceiptService(db, new FakeFileStorage(), new FakeQueue());

        var created = await service.CreateAsync(userId, JpegBytes, "receipt.jpg");
        var withItem = await service.AddItemAsync(userId, created.Id, new CreateReceiptItemRequest("Хлеб", 2, 3m, CategoryId: expenseId));

        var confirmed = await service.ConfirmAsync(userId, created.Id);

        Assert.True(confirmed.Confirmed);
        Assert.Equal(6m, confirmed.TotalAmount);

        var transaction = Assert.Single(db.Transactions);
        Assert.Equal(TransactionSource.Receipt, transaction.Source);
        Assert.Equal(created.Id, transaction.ReceiptId);
        Assert.Equal(6m, transaction.Amount);
        Assert.Equal(expenseId, transaction.CategoryId);
    }

    [Fact]
    public async Task Confirm_AlreadyConfirmed_Throws()
    {
        var (db, userId, _, _) = await SeedAsync();
        await using var _1 = db;
        var service = new ReceiptService(db, new FakeFileStorage(), new FakeQueue());

        var created = await service.CreateAsync(userId, JpegBytes, "receipt.jpg");
        await service.AddItemAsync(userId, created.Id, new CreateReceiptItemRequest("Хлеб", 1, 1m));
        await service.ConfirmAsync(userId, created.Id);

        await Assert.ThrowsAsync<ValidationException>(() => service.ConfirmAsync(userId, created.Id));
    }

    [Fact]
    public async Task AddItem_AfterConfirm_Throws()
    {
        var (db, userId, _, _) = await SeedAsync();
        await using var _1 = db;
        var service = new ReceiptService(db, new FakeFileStorage(), new FakeQueue());

        var created = await service.CreateAsync(userId, JpegBytes, "receipt.jpg");
        await service.AddItemAsync(userId, created.Id, new CreateReceiptItemRequest("Хлеб", 1, 1m));
        await service.ConfirmAsync(userId, created.Id);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.AddItemAsync(userId, created.Id, new CreateReceiptItemRequest("Ещё", 1, 1m)));
    }

    [Fact]
    public async Task GetImage_ReturnsStoredBytesAndContentType()
    {
        var (db, userId, _, _) = await SeedAsync();
        await using var _1 = db;
        var service = new ReceiptService(db, new FakeFileStorage(), new FakeQueue());

        var created = await service.CreateAsync(userId, JpegBytes, "receipt.jpg");
        var image = await service.GetImageAsync(userId, created.Id);

        Assert.Equal("image/jpeg", image.ContentType);
        Assert.Equal(JpegBytes, image.Content);
    }

    [Fact]
    public async Task Get_OtherUserReceipt_Throws()
    {
        var (db, userId, _, _) = await SeedAsync();
        await using var _1 = db;
        var service = new ReceiptService(db, new FakeFileStorage(), new FakeQueue());

        var created = await service.CreateAsync(userId, JpegBytes, "receipt.jpg");

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(Guid.NewGuid(), created.Id));
    }

    [Fact]
    public async Task GetMatches_FindsSimilarManualOperation()
    {
        var (db, userId, _, _) = await SeedAsync();
        await using var _1 = db;
        var account = await db.Accounts.FirstAsync();
        var service = new ReceiptService(db, new FakeFileStorage(), new FakeQueue());

        var created = await service.CreateAsync(userId, JpegBytes, "receipt.jpg");
        await service.AddItemAsync(userId, created.Id, new CreateReceiptItemRequest("Товар", 1, 10.88m));

        db.Transactions.Add(new Transaction
        {
            UserId = userId,
            AccountId = account.Id,
            Type = TransactionType.Expense,
            Amount = 10.88m,
            OccurredAt = DateTimeOffset.UtcNow,
            Source = TransactionSource.Manual,
            Comment = "кола чипсы"
        });
        await db.SaveChangesAsync();

        var matches = await service.GetMatchesAsync(userId, created.Id);

        Assert.Single(matches);
        Assert.Equal("кола чипсы", matches[0].Comment);
    }

    [Fact]
    public async Task GetMatches_IgnoresDifferentAmountAndSource()
    {
        var (db, userId, _, _) = await SeedAsync();
        await using var _1 = db;
        var account = await db.Accounts.FirstAsync();
        var service = new ReceiptService(db, new FakeFileStorage(), new FakeQueue());

        var created = await service.CreateAsync(userId, JpegBytes, "receipt.jpg");
        await service.AddItemAsync(userId, created.Id, new CreateReceiptItemRequest("Товар", 1, 10.88m));

        db.Transactions.Add(new Transaction
        {
            UserId = userId,
            AccountId = account.Id,
            Type = TransactionType.Expense,
            Amount = 99m,
            OccurredAt = DateTimeOffset.UtcNow,
            Source = TransactionSource.Manual
        });
        await db.SaveChangesAsync();

        Assert.Empty(await service.GetMatchesAsync(userId, created.Id));
    }

    [Fact]
    public async Task Link_AttachesReceiptAndBlocksEdits()
    {
        var (db, userId, _, _) = await SeedAsync();
        await using var _1 = db;
        var account = await db.Accounts.FirstAsync();
        var service = new ReceiptService(db, new FakeFileStorage(), new FakeQueue());

        var created = await service.CreateAsync(userId, JpegBytes, "receipt.jpg");
        await service.AddItemAsync(userId, created.Id, new CreateReceiptItemRequest("Товар", 1, 10.88m));

        var manual = new Transaction
        {
            UserId = userId,
            AccountId = account.Id,
            Type = TransactionType.Expense,
            Amount = 10.88m,
            OccurredAt = DateTimeOffset.UtcNow,
            Source = TransactionSource.Manual
        };
        db.Transactions.Add(manual);
        await db.SaveChangesAsync();

        var result = await service.LinkAsync(userId, created.Id, manual.Id);

        Assert.True(result.Confirmed);
        Assert.Equal(created.Id, manual.ReceiptId);
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.AddItemAsync(userId, created.Id, new CreateReceiptItemRequest("Ещё", 1, 1m)));
    }

    [Fact]
    public async Task Delete_Unconfirmed_RemovesReceiptItemsAndFile()
    {
        var (db, userId, _, _) = await SeedAsync();
        await using var _1 = db;
        var storage = new FakeFileStorage();
        var service = new ReceiptService(db, storage, new FakeQueue());

        var created = await service.CreateAsync(userId, JpegBytes, "receipt.jpg");
        var path = (await db.Receipts.FindAsync(created.Id))!.ImagePath;
        await service.AddItemAsync(userId, created.Id, new CreateReceiptItemRequest("Товар", 1, 2m));

        await service.DeleteAsync(userId, created.Id);

        Assert.Null(await db.Receipts.FindAsync(created.Id));
        Assert.Equal(0, await db.ReceiptItems.CountAsync(i => i.ReceiptId == created.Id));
        Assert.Contains(path, storage.Deleted);
    }

    [Fact]
    public async Task Delete_Confirmed_RemovesCreatedTransactions()
    {
        var (db, userId, _, _) = await SeedAsync();
        await using var _1 = db;
        var service = new ReceiptService(db, new FakeFileStorage(), new FakeQueue());

        var created = await service.CreateAsync(userId, JpegBytes, "receipt.jpg");
        await service.AddItemAsync(userId, created.Id, new CreateReceiptItemRequest("Товар", 1, 2m));
        await service.ConfirmAsync(userId, created.Id);

        Assert.Equal(1, await db.Transactions.CountAsync());

        await service.DeleteAsync(userId, created.Id);

        Assert.Equal(0, await db.Transactions.CountAsync());
        Assert.Null(await db.Receipts.FindAsync(created.Id));
    }

    [Fact]
    public async Task Delete_Linked_KeepsManualTransaction()
    {
        var (db, userId, _, _) = await SeedAsync();
        await using var _1 = db;
        var account = await db.Accounts.FirstAsync();
        var service = new ReceiptService(db, new FakeFileStorage(), new FakeQueue());

        var created = await service.CreateAsync(userId, JpegBytes, "receipt.jpg");
        await service.AddItemAsync(userId, created.Id, new CreateReceiptItemRequest("Товар", 1, 10.88m));

        var manual = new Transaction
        {
            UserId = userId,
            AccountId = account.Id,
            Type = TransactionType.Expense,
            Amount = 10.88m,
            OccurredAt = DateTimeOffset.UtcNow,
            Source = TransactionSource.Manual
        };
        db.Transactions.Add(manual);
        await db.SaveChangesAsync();

        await service.LinkAsync(userId, created.Id, manual.Id);
        await service.DeleteAsync(userId, created.Id);

        var reloaded = await db.Transactions.FindAsync(manual.Id);
        Assert.NotNull(reloaded);
        Assert.Null(reloaded!.ReceiptId);
    }
}
