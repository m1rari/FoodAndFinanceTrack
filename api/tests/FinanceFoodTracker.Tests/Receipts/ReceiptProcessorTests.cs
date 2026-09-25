using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Common.Options;
using FinanceFoodTracker.Application.Receipts;
using FinanceFoodTracker.Application.Receipts.Analysis;
using FinanceFoodTracker.Domain.Entities;
using FinanceFoodTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FinanceFoodTracker.Tests.Receipts;

public sealed class ReceiptProcessorTests
{
    private sealed class FakeAnalyzer : IReceiptAnalyzer
    {
        private readonly ReceiptAnalysisResult _result;
        private readonly Exception? _error;

        public FakeAnalyzer(ReceiptAnalysisResult? result = null, Exception? error = null)
        {
            _result = result ?? new ReceiptAnalysisResult();
            _error = error;
        }

        public ReceiptAnalysisRequest? LastRequest { get; private set; }

        public Task<ReceiptAnalysisResult> AnalyzeAsync(ReceiptAnalysisRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;

            if (_error is not null)
            {
                throw _error;
            }

            return Task.FromResult(_result);
        }
    }

    private sealed class FakeTelegramBot : ITelegramBot
    {
        public Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<byte[]?> DownloadFileAsync(string fileId, CancellationToken cancellationToken = default)
            => Task.FromResult<byte[]?>(null);

        public Task SetWebhookAsync(string url, string? secretToken, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private static ReceiptProcessor CreateProcessor(Infrastructure.Persistence.AppDbContext db, IReceiptAnalyzer analyzer)
        => new(db, analyzer, new FakeTelegramBot(), Options.Create(new AiOptions { ConfidenceThreshold = 0.6m }), NullLogger<ReceiptProcessor>.Instance);

    private static async Task<(Infrastructure.Persistence.AppDbContext Db, Receipt Receipt, Guid ExpenseCategoryId)> SeedAsync()
    {
        var db = TestDb.Create();
        var user = new User { TelegramId = 888, Username = "processor" };
        var expense = new Category { Name = "Продукты", Type = TransactionType.Expense, IsSystem = true };
        var income = new Category { Name = "Зарплата", Type = TransactionType.Income, IsSystem = true };
        var receipt = new Receipt { UserId = user.Id, ImagePath = "2026/09/receipt.jpg", Status = ProcessingStatus.Pending };

        db.Users.Add(user);
        db.Categories.AddRange(expense, income);
        db.Receipts.Add(receipt);
        await db.SaveChangesAsync();

        return (db, receipt, expense.Id);
    }

    [Fact]
    public async Task Process_ValidAnalysis_SetsProcessedAndMapsCategories()
    {
        var (db, receipt, expenseId) = await SeedAsync();
        await using var _ = db;
        var analysis = new ReceiptAnalysisResult(
            MerchantName: "Евроопт",
            TotalAmount: 10m,
            Confidence: 0.9m,
            RawOcrText: "текст",
            RawResponse: "{\"ok\":true}",
            Items: new[] { new ReceiptAnalysisItem("Молоко", 2, 5m, 10m, "Продукты", 0.9m) });
        var analyzer = new FakeAnalyzer(analysis);
        var processor = CreateProcessor(db, analyzer);

        await processor.ProcessAsync(receipt.Id);

        Assert.Equal(ProcessingStatus.Processed, receipt.Status);
        Assert.Equal("Евроопт", receipt.MerchantName);
        Assert.Equal(10m, receipt.TotalAmount);
        Assert.Equal("{\"ok\":true}", receipt.AiRawResponse);
        Assert.Contains("Продукты", analyzer.LastRequest!.Categories);

        var item = await db.ReceiptItems.SingleAsync(i => i.ReceiptId == receipt.Id);
        Assert.Equal(expenseId, item.CategoryId);
        Assert.Equal(10m, item.TotalPrice);
    }

    [Fact]
    public async Task Process_LowConfidence_NeedsReview()
    {
        var (db, receipt, _) = await SeedAsync();
        await using var _ = db;
        var analysis = new ReceiptAnalysisResult(
            TotalAmount: 5m,
            Confidence: 0.3m,
            Items: new[] { new ReceiptAnalysisItem("Хлеб", 1, 5m, 5m, "Продукты", 0.9m) });
        var processor = CreateProcessor(db, new FakeAnalyzer(analysis));

        await processor.ProcessAsync(receipt.Id);

        Assert.Equal(ProcessingStatus.NeedsReview, receipt.Status);
    }

    [Fact]
    public async Task Process_SumMismatch_NeedsReview()
    {
        var (db, receipt, _) = await SeedAsync();
        await using var _ = db;
        var analysis = new ReceiptAnalysisResult(
            TotalAmount: 99m,
            Confidence: 0.9m,
            Items: new[] { new ReceiptAnalysisItem("Хлеб", 1, 5m, 5m, "Продукты", 0.9m) });
        var processor = CreateProcessor(db, new FakeAnalyzer(analysis));

        await processor.ProcessAsync(receipt.Id);

        Assert.Equal(ProcessingStatus.NeedsReview, receipt.Status);
    }

    [Fact]
    public async Task Process_EmptyItems_NeedsReview()
    {
        var (db, receipt, _) = await SeedAsync();
        await using var _ = db;
        var processor = CreateProcessor(db, new FakeAnalyzer(new ReceiptAnalysisResult()));

        await processor.ProcessAsync(receipt.Id);

        Assert.Equal(ProcessingStatus.NeedsReview, receipt.Status);
    }

    [Fact]
    public async Task Process_AnalyzerFailure_SetsFailed()
    {
        var (db, receipt, _) = await SeedAsync();
        await using var _ = db;
        var processor = CreateProcessor(db, new FakeAnalyzer(error: new InvalidOperationException("provider down")));

        await processor.ProcessAsync(receipt.Id);

        Assert.Equal(ProcessingStatus.Failed, receipt.Status);
    }

    [Fact]
    public async Task Process_InvalidItems_AreSkipped()
    {
        var (db, receipt, _) = await SeedAsync();
        await using var _ = db;
        var analysis = new ReceiptAnalysisResult(
            TotalAmount: 4m,
            Confidence: 0.9m,
            Items: new[]
            {
                new ReceiptAnalysisItem(" ", 1, 4m, 4m, "Продукты", 0.9m),
                new ReceiptAnalysisItem("Кефир", 1, 4m, 4m, "Продукты", 0.9m)
            });
        var processor = CreateProcessor(db, new FakeAnalyzer(analysis));

        await processor.ProcessAsync(receipt.Id);

        var items = await db.ReceiptItems.Where(i => i.ReceiptId == receipt.Id).ToListAsync();
        Assert.Single(items);
        Assert.Equal("Кефир", items[0].Name);
        Assert.Equal(ProcessingStatus.NeedsReview, receipt.Status);
    }

    [Fact]
    public async Task Process_AlreadyProcessed_Skips()
    {
        var (db, receipt, _) = await SeedAsync();
        await using var _ = db;
        receipt.Status = ProcessingStatus.Processed;
        await db.SaveChangesAsync();
        var analyzer = new FakeAnalyzer(new ReceiptAnalysisResult());

        var processor = CreateProcessor(db, analyzer);
        await processor.ProcessAsync(receipt.Id);

        Assert.Null(analyzer.LastRequest);
    }
}
