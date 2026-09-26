using System.Text;
using FinanceFoodTracker.Application.Common.Exceptions;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Statements;
using FinanceFoodTracker.Application.Statements.Analysis;
using FinanceFoodTracker.Domain.Entities;
using FinanceFoodTracker.Domain.Enums;
using Xunit;

namespace FinanceFoodTracker.Tests.Statements;

public sealed class StatementServiceTests
{
    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4 fake statement");

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

        public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
        {
            _files.Remove(path);
            return Task.CompletedTask;
        }

        public bool Exists(string path) => _files.ContainsKey(path);
    }

    private sealed class FakePdf : IPdfTextExtractor
    {
        public string Extract(byte[] pdf) => "statement text";
    }

    private sealed class FakeAnalyzer : IStatementAnalyzer
    {
        private readonly IReadOnlyList<StatementOperationResult> _operations;

        public FakeAnalyzer(IReadOnlyList<StatementOperationResult>? operations = null)
        {
            _operations = operations ?? Array.Empty<StatementOperationResult>();
        }

        public Task<StatementAnalysisResult> AnalyzeAsync(StatementAnalysisRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new StatementAnalysisResult("BYN", 100m, 90m, _operations, "{\"ok\":true}"));
    }

    private static async Task<(Infrastructure.Persistence.AppDbContext Db, Guid UserId)> SeedAsync()
    {
        var db = TestDb.Create();
        var user = new User { TelegramId = 1234, Username = "statement-user" };
        db.Users.Add(user);
        db.Accounts.Add(new Account { UserId = user.Id, Name = "Основной", Currency = "BYN" });
        db.Categories.Add(new Category { Name = "Продукты", Type = TransactionType.Expense, IsSystem = true });
        db.Categories.Add(new Category { Name = "Зарплата", Type = TransactionType.Income, IsSystem = true });
        await db.SaveChangesAsync();
        return (db, user.Id);
    }

    private static StatementService CreateService(
        Infrastructure.Persistence.AppDbContext db,
        IReadOnlyList<StatementOperationResult>? operations = null)
        => new(db, new FakeFileStorage(), new FakePdf(), new FakeAnalyzer(operations));

    [Fact]
    public async Task Create_ValidPdf_ParsesAndMapsCategory()
    {
        var (db, userId) = await SeedAsync();
        await using var _ = db;
        var service = CreateService(db, new[]
        {
            new StatementOperationResult(new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.FromHours(3)), 49.06m, "expense", "Оплата товаров", "MINSK", "BYN", "5411", false, "Продукты", 0.9m)
        });

        var result = await service.CreateAsync(userId, PdfBytes, "statement.pdf");

        Assert.Equal("Processed", result.Status);
        var operation = Assert.Single(result.Operations);
        Assert.Equal("Продукты", operation.CategoryName);
        Assert.NotNull(operation.CategoryId);
    }

    [Fact]
    public async Task Create_UnsupportedFile_Throws()
    {
        var (db, userId) = await SeedAsync();
        await using var _ = db;
        var service = CreateService(db);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(userId, new byte[] { 0x25, 0x50, 0x44 }, "file.txt"));
    }

    [Fact]
    public async Task Confirm_CreatesTransactionsAndFlagsTransfer()
    {
        var (db, userId) = await SeedAsync();
        await using var _ = db;
        var service = CreateService(db);

        var created = await service.CreateAsync(userId, PdfBytes, "statement.pdf");
        var transfer = new StatementOperationDto(new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.FromHours(3)), 300m, "expense", "Перевод P2P", null, "BYN", "6012", true, null, null, "Перевод P2P", 0.8m);
        var purchase = new StatementOperationDto(new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.FromHours(3)), 49.06m, "expense", "Оплата товаров", null, "BYN", "5411", false, null, null, "Продукты", 0.9m);

        var result = await service.ConfirmAsync(userId, created.Id, new ConfirmStatementRequest(new[] { transfer, purchase }));

        Assert.True(result.Confirmed);
        Assert.Equal(2, result.CreatedCount);

        var transactions = db.Transactions.ToList();
        Assert.Equal(2, transactions.Count);
        Assert.All(transactions, t => Assert.Equal(TransactionSource.Statement, t.Source));
        Assert.Single(transactions, t => t.IsTransfer);
    }

    [Fact]
    public async Task Confirm_Twice_Throws()
    {
        var (db, userId) = await SeedAsync();
        await using var _ = db;
        var service = CreateService(db);
        var created = await service.CreateAsync(userId, PdfBytes, "statement.pdf");

        await service.ConfirmAsync(userId, created.Id, new ConfirmStatementRequest(Array.Empty<StatementOperationDto>()));

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.ConfirmAsync(userId, created.Id, new ConfirmStatementRequest(Array.Empty<StatementOperationDto>())));
    }
}
