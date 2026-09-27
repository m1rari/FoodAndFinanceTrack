using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Common.Options;
using FinanceFoodTracker.Application.FoodLogs;
using FinanceFoodTracker.Application.FoodLogs.Analysis;
using FinanceFoodTracker.Application.SavedDishes;
using FinanceFoodTracker.Domain.Entities;
using FinanceFoodTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FinanceFoodTracker.Tests.FoodLogs;

public sealed class FoodLogProcessorTests
{
    private sealed class FakeAnalyzer : IFoodImageAnalyzer
    {
        private readonly IReadOnlyList<FoodAnalysisItemResult> _items;
        private readonly Exception? _error;

        public FakeAnalyzer(IReadOnlyList<FoodAnalysisItemResult>? items = null, Exception? error = null)
        {
            _items = items ?? Array.Empty<FoodAnalysisItemResult>();
            _error = error;
        }

        public FoodAnalysisRequest? LastRequest { get; private set; }

        public Task<FoodAnalysisResult> AnalyzeAsync(FoodAnalysisRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;

            if (_error is not null)
            {
                throw _error;
            }

            return Task.FromResult(new FoodAnalysisResult(_items));
        }
    }

    private sealed class FakeTelegramBot : ITelegramBot
    {
        public Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendKeyboardAsync(long chatId, string text, IReadOnlyList<IReadOnlyList<string>> rows, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<byte[]?> DownloadFileAsync(string fileId, CancellationToken cancellationToken = default) => Task.FromResult<byte[]?>(null);

        public Task SetWebhookAsync(string url, string? secretToken, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static FoodAnalysisItemResult Item(
        string name,
        decimal? calMin,
        decimal? calMax,
        decimal? proteinMin = null,
        decimal? proteinMax = null,
        decimal? portion = null,
        decimal? confidence = 0.9m)
        => new(name, portion, calMin, calMax, proteinMin, proteinMax, null, null, null, null, confidence);

    private static FoodLogProcessor CreateProcessor(Infrastructure.Persistence.AppDbContext db, IFoodImageAnalyzer analyzer)
        => new(db, analyzer, new FakeTelegramBot(), new SavedDishService(db), Options.Create(new AiOptions { ConfidenceThreshold = 0.6m }), NullLogger<FoodLogProcessor>.Instance);

    private static async Task<(Infrastructure.Persistence.AppDbContext Db, FoodLog Log)> SeedAsync()
    {
        var db = TestDb.Create();
        var user = new User { TelegramId = 1000, Username = "processor-food" };
        var log = new FoodLog { UserId = user.Id, ImagePath = "2026/09/dish.jpg", Status = ProcessingStatus.Pending };

        db.Users.Add(user);
        db.FoodLogs.Add(log);
        await db.SaveChangesAsync();

        return (db, log);
    }

    [Fact]
    public async Task Process_ValidAnalysis_SetsProcessedAndMidpoints()
    {
        var (db, log) = await SeedAsync();
        await using var _ = db;
        var processor = CreateProcessor(db, new FakeAnalyzer(new[] { Item("Паста", 400, 600, 10, 20, 250) }));

        await processor.ProcessAsync(log.Id);

        Assert.Equal(ProcessingStatus.Processed, log.Status);
        Assert.Equal("Паста", log.DishName);
        Assert.Equal(250m, log.PortionGrams);
        Assert.Equal(15m, log.ProteinG);
    }

    [Fact]
    public async Task Process_PassesUserContextToAnalyzer()
    {
        var (db, log) = await SeedAsync();
        await using var _ = db;
        log.UserContext = "домашняя паста с курицей";
        await db.SaveChangesAsync();
        var analyzer = new FakeAnalyzer(new[] { Item("Паста", 400, 600) });

        await CreateProcessor(db, analyzer).ProcessAsync(log.Id);

        Assert.Equal("домашняя паста с курицей", analyzer.LastRequest!.Context);
    }

    [Fact]
    public async Task Process_MultipleItems_SplitsIntoSeparateLogs()
    {
        var (db, log) = await SeedAsync();
        await using var _ = db;
        var analyzer = new FakeAnalyzer(new[] { Item("Борщ", 200, 300), Item("Пюре с курицей", 400, 500) });
        var processor = CreateProcessor(db, analyzer);

        await processor.ProcessAsync(log.Id);

        var logs = await db.FoodLogs.ToListAsync();
        Assert.Equal(2, logs.Count);
        Assert.NotNull(log.MealGroupId);
        Assert.All(logs, item => Assert.Equal(log.MealGroupId, item.MealGroupId));
        Assert.Contains(logs, item => item.DishName == "Пюре с курицей");
    }

    [Fact]
    public async Task Process_Single_DoesNotSplit()
    {
        var (db, log) = await SeedAsync();
        await using var _ = db;
        var analyzer = new FakeAnalyzer(new[] { Item("Борщ", 200, 300), Item("Пюре", 400, 500) });
        var processor = CreateProcessor(db, analyzer);

        await processor.ProcessAsync(log.Id, single: true);

        Assert.Single(await db.FoodLogs.ToListAsync());
        Assert.Equal("Борщ", log.DishName);
    }

    [Fact]
    public async Task Process_LowConfidence_NeedsReview()
    {
        var (db, log) = await SeedAsync();
        await using var _ = db;
        var processor = CreateProcessor(db, new FakeAnalyzer(new[] { Item("Салат", 100, 200, confidence: 0.2m) }));

        await processor.ProcessAsync(log.Id);

        Assert.Equal(ProcessingStatus.NeedsReview, log.Status);
    }

    [Fact]
    public async Task Process_Empty_NeedsReview()
    {
        var (db, log) = await SeedAsync();
        await using var _ = db;

        await CreateProcessor(db, new FakeAnalyzer()).ProcessAsync(log.Id);

        Assert.Equal(ProcessingStatus.NeedsReview, log.Status);
    }

    [Fact]
    public async Task Process_PlaceholderAnswer_NeedsReview()
    {
        var (db, log) = await SeedAsync();
        await using var _ = db;
        var processor = CreateProcessor(db, new FakeAnalyzer(new[] { Item("Не определено", 0, 0) }));

        await processor.ProcessAsync(log.Id);

        Assert.Equal(ProcessingStatus.NeedsReview, log.Status);
    }

    [Fact]
    public async Task Process_AnalyzerFailure_SetsFailed()
    {
        var (db, log) = await SeedAsync();
        await using var _ = db;
        var processor = CreateProcessor(db, new FakeAnalyzer(error: new InvalidOperationException("down")));

        await processor.ProcessAsync(log.Id);

        Assert.Equal(ProcessingStatus.Failed, log.Status);
    }

    [Fact]
    public async Task Process_AlreadyProcessed_Skips()
    {
        var (db, log) = await SeedAsync();
        await using var _ = db;
        log.Status = ProcessingStatus.Processed;
        await db.SaveChangesAsync();
        var analyzer = new FakeAnalyzer();

        await CreateProcessor(db, analyzer).ProcessAsync(log.Id);

        Assert.Null(analyzer.LastRequest);
    }
}
