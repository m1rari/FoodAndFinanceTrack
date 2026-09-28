using System.Text.Json;
using FinanceFoodTracker.Application.Statements;
using FinanceFoodTracker.Application.Statements.Analysis;
using FinanceFoodTracker.Domain.Entities;
using FinanceFoodTracker.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FinanceFoodTracker.Tests.Statements;

public sealed class StatementProcessorTests
{
    private sealed class FakeAnalyzer : IStatementAnalyzer
    {
        private readonly IReadOnlyList<StatementOperationResult> _operations;
        private readonly Exception? _error;

        public FakeAnalyzer(IReadOnlyList<StatementOperationResult>? operations = null, Exception? error = null)
        {
            _operations = operations ?? Array.Empty<StatementOperationResult>();
            _error = error;
        }

        public Task<StatementAnalysisResult> AnalyzeAsync(StatementAnalysisRequest request, CancellationToken cancellationToken = default)
        {
            if (_error is not null)
            {
                throw _error;
            }

            return Task.FromResult(new StatementAnalysisResult("BYN", null, null, _operations, "{\"ok\":true}"));
        }
    }

    private static StatementProcessor CreateProcessor(Infrastructure.Persistence.AppDbContext db, IStatementAnalyzer analyzer)
        => new(db, analyzer, NullLogger<StatementProcessor>.Instance);

    private static async Task<(Infrastructure.Persistence.AppDbContext Db, Statement Statement)> SeedAsync()
    {
        var db = TestDb.Create();
        var user = new User { TelegramId = 55, Username = "processor-statement" };
        var statement = new Statement { UserId = user.Id, FileName = "s.pdf", PdfPath = "x.pdf", RawText = "text", Status = ProcessingStatus.Pending };

        db.Users.Add(user);
        db.Categories.Add(new Category { Name = "Продукты", Type = TransactionType.Expense, IsSystem = true });
        db.Statements.Add(statement);
        await db.SaveChangesAsync();

        return (db, statement);
    }

    [Fact]
    public async Task Process_ParsesOperationsAndMapsCategory()
    {
        var (db, statement) = await SeedAsync();
        await using var _ = db;
        var analyzer = new FakeAnalyzer(new[]
        {
            new StatementOperationResult(
                new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.FromHours(3)),
                49.06m, "expense", "SHOP", "MINSK", "BYN", "5411", false, "Продукты", 0.9m)
        });
        var processor = CreateProcessor(db, analyzer);

        await processor.ProcessAsync(statement.Id);

        Assert.Equal(ProcessingStatus.Processed, statement.Status);

        var operations = JsonSerializer.Deserialize<List<StatementOperationDto>>(
            statement.ParsedOperations!,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var operation = Assert.Single(operations!);
        Assert.Equal("Продукты", operation.CategoryName);
        Assert.NotNull(operation.CategoryId);
    }

    [Fact]
    public async Task Process_EmptyOperations_NeedsReview()
    {
        var (db, statement) = await SeedAsync();
        await using var _ = db;

        await CreateProcessor(db, new FakeAnalyzer()).ProcessAsync(statement.Id);

        Assert.Equal(ProcessingStatus.NeedsReview, statement.Status);
    }

    [Fact]
    public async Task Process_AnalyzerFailure_SetsFailed()
    {
        var (db, statement) = await SeedAsync();
        await using var _ = db;

        await CreateProcessor(db, new FakeAnalyzer(error: new InvalidOperationException("down"))).ProcessAsync(statement.Id);

        Assert.Equal(ProcessingStatus.Failed, statement.Status);
    }

    [Fact]
    public async Task Process_AlreadyProcessed_Skips()
    {
        var (db, statement) = await SeedAsync();
        await using var _ = db;
        statement.Status = ProcessingStatus.Processed;
        await db.SaveChangesAsync();

        await CreateProcessor(db, new FakeAnalyzer(error: new InvalidOperationException("should not run"))).ProcessAsync(statement.Id);

        Assert.Equal(ProcessingStatus.Processed, statement.Status);
    }
}
