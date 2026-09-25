using FinanceFoodTracker.Application.Common.Options;
using FinanceFoodTracker.Application.FoodLogs;
using FinanceFoodTracker.Application.FoodLogs.Analysis;
using FinanceFoodTracker.Domain.Entities;
using FinanceFoodTracker.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FinanceFoodTracker.Tests.FoodLogs;

public sealed class FoodLogProcessorTests
{
    private sealed class FakeAnalyzer : IFoodImageAnalyzer
    {
        private readonly FoodAnalysisResult _result;
        private readonly Exception? _error;

        public FakeAnalyzer(FoodAnalysisResult? result = null, Exception? error = null)
        {
            _result = result ?? new FoodAnalysisResult();
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

            return Task.FromResult(_result);
        }
    }

    private static FoodLogProcessor CreateProcessor(Infrastructure.Persistence.AppDbContext db, IFoodImageAnalyzer analyzer)
        => new(db, analyzer, Options.Create(new AiOptions { ConfidenceThreshold = 0.6m }), NullLogger<FoodLogProcessor>.Instance);

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
        var analysis = new FoodAnalysisResult(
            DishName: "Паста",
            CaloriesMin: 400,
            CaloriesMax: 600,
            ProteinMinG: 10,
            ProteinMaxG: 20,
            Confidence: 0.9m,
            RawResponse: "{\"ok\":true}");
        var processor = CreateProcessor(db, new FakeAnalyzer(analysis));

        await processor.ProcessAsync(log.Id);

        Assert.Equal(ProcessingStatus.Processed, log.Status);
        Assert.Equal("Паста", log.DishName);
        Assert.Equal(500m, (log.CaloriesMin!.Value + log.CaloriesMax!.Value) / 2m);
        Assert.Equal(15m, log.ProteinG);
        Assert.Equal("{\"ok\":true}", log.AiRawResponse);
    }

    [Fact]
    public async Task Process_PassesUserContextToAnalyzer()
    {
        var (db, log) = await SeedAsync();
        await using var _ = db;
        log.UserContext = "домашняя паста с курицей";
        await db.SaveChangesAsync();
        var analyzer = new FakeAnalyzer(new FoodAnalysisResult(DishName: "Паста", CaloriesMin: 400, CaloriesMax: 600));
        var processor = CreateProcessor(db, analyzer);

        await processor.ProcessAsync(log.Id);

        Assert.Equal("домашняя паста с курицей", analyzer.LastRequest!.Context);
    }

    [Fact]
    public async Task Process_LowConfidence_NeedsReview()
    {
        var (db, log) = await SeedAsync();
        await using var _ = db;
        var processor = CreateProcessor(db, new FakeAnalyzer(new FoodAnalysisResult(DishName: "Салат", CaloriesMin: 100, CaloriesMax: 200, Confidence: 0.2m)));

        await processor.ProcessAsync(log.Id);

        Assert.Equal(ProcessingStatus.NeedsReview, log.Status);
    }

    [Fact]
    public async Task Process_Empty_NeedsReview()
    {
        var (db, log) = await SeedAsync();
        await using var _ = db;
        var processor = CreateProcessor(db, new FakeAnalyzer());

        await processor.ProcessAsync(log.Id);

        Assert.Equal(ProcessingStatus.NeedsReview, log.Status);
    }

    [Fact]
    public async Task Process_PlaceholderAnswer_NeedsReview()
    {
        var (db, log) = await SeedAsync();
        await using var _ = db;
        var processor = CreateProcessor(db, new FakeAnalyzer(new FoodAnalysisResult(
            DishName: "Не определено",
            CaloriesMin: 0,
            CaloriesMax: 0,
            Confidence: 0.9m)));

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
