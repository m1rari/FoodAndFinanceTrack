using FinanceFoodTracker.Application.Common.Exceptions;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Receipts;
using FinanceFoodTracker.Application.Receipts.Analysis;
using FinanceFoodTracker.Domain.Entities;
using FinanceFoodTracker.Domain.Enums;
using Xunit;

namespace FinanceFoodTracker.Tests.Receipts;

public sealed class ReceiptServiceTests
{
    private static readonly byte[] JpegBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };

    private sealed class FakeFileStorage : IFileStorage
    {
        private readonly Dictionary<string, byte[]> _files = new();

        public Task<string> SaveAsync(byte[] content, string extension, CancellationToken cancellationToken = default)
        {
            var path = $"{Guid.NewGuid():N}{extension}";
            _files[path] = content;
            return Task.FromResult(path);
        }

        public Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken = default)
            => Task.FromResult(_files[path]);

        public bool Exists(string path) => _files.ContainsKey(path);
    }

    private sealed class FakeAnalyzer : IReceiptAnalyzer
    {
        private readonly ReceiptAnalysisResult _result;

        public FakeAnalyzer(ReceiptAnalysisResult? result = null)
        {
            _result = result ?? new ReceiptAnalysisResult();
        }

        public Task<ReceiptAnalysisResult> AnalyzeAsync(string imagePath, CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }

    private static async Task<(Infrastructure.Persistence.AppDbContext Db, Guid UserId, Guid ExpenseCategoryId, Guid IncomeCategoryId)> SeedAsync()
    {
        var db = TestDb.Create();
        var user = new User { TelegramId = 777, Username = "receipt-user" };
        var expense = new Category { Name = "Продукты", Type = TransactionType.Expense, IsSystem = true };
        var income = new Category { Name = "Зарплата", Type = TransactionType.Income, IsSystem = true };

        db.Users.Add(user);
        db.Categories.AddRange(expense, income);
        await db.SaveChangesAsync();

        return (db, user.Id, expense.Id, income.Id);
    }

    [Fact]
    public async Task Create_ValidJpeg_SavesImageAndNeedsReview()
    {
        var (db, userId, _, _) = await SeedAsync();
        await using var _1 = db;
        var storage = new FakeFileStorage();
        var service = new ReceiptService(db, storage, new FakeAnalyzer());

        var result = await service.CreateAsync(userId, JpegBytes, "receipt.jpg");

        Assert.Equal("NeedsReview", result.Status);
        Assert.Empty(result.Items);
        Assert.Equal($"/api/receipts/{result.Id}/image", result.ImageUrl);

        var receipt = await db.Receipts.FindAsync(result.Id);
        Assert.NotNull(receipt);
        Assert.True(storage.Exists(receipt!.ImagePath));
    }

    [Fact]
    public async Task Create_UnsupportedContent_Throws()
    {
        var (db, userId, _, _) = await SeedAsync();
        await using var _1 = db;
        var service = new ReceiptService(db, new FakeFileStorage(), new FakeAnalyzer());

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(userId, new byte[] { 0x25, 0x50, 0x44, 0x46 }, "file.pdf"));
    }

    [Fact]
    public async Task Create_WhenAnalyzerReturnsItems_Processes()
    {
        var (db, userId, _, _) = await SeedAsync();
        await using var _1 = db;
        var analysis = new ReceiptAnalysisResult(
            MerchantName: "Евроопт",
            TotalAmount: 5.40m,
            Confidence: 0.9m,
            Items: new[] { new ReceiptAnalysisItem("Молоко", 2, 2.70m, 5.40m, Confidence: 0.9m) });
        var service = new ReceiptService(db, new FakeFileStorage(), new FakeAnalyzer(analysis));

        var result = await service.CreateAsync(userId, JpegBytes, "receipt.jpg");

        Assert.Equal("Processed", result.Status);
        Assert.Equal("Евроопт", result.MerchantName);
        Assert.Single(result.Items);
        Assert.Equal("Молоко", result.Items[0].Name);
    }

    [Fact]
    public async Task AddItem_UpdatesTotalAndStatus()
    {
        var (db, userId, expenseId, _) = await SeedAsync();
        await using var _1 = db;
        var service = new ReceiptService(db, new FakeFileStorage(), new FakeAnalyzer());

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
        var service = new ReceiptService(db, new FakeFileStorage(), new FakeAnalyzer());

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
        var service = new ReceiptService(db, new FakeFileStorage(), new FakeAnalyzer());

        var created = await service.CreateAsync(userId, JpegBytes, "receipt.jpg");
        var withItem = await service.AddItemAsync(userId, created.Id, new CreateReceiptItemRequest("Хлеб", 1, 1.25m));
        var itemId = withItem.Items[0].Id;

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateItemAsync(userId, created.Id, itemId, new UpdateReceiptItemRequest(CategoryId: incomeId)));
    }

    [Fact]
    public async Task GetImage_ReturnsStoredBytesAndContentType()
    {
        var (db, userId, _, _) = await SeedAsync();
        await using var _1 = db;
        var service = new ReceiptService(db, new FakeFileStorage(), new FakeAnalyzer());

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
        var service = new ReceiptService(db, new FakeFileStorage(), new FakeAnalyzer());

        var created = await service.CreateAsync(userId, JpegBytes, "receipt.jpg");

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(Guid.NewGuid(), created.Id));
    }
}
