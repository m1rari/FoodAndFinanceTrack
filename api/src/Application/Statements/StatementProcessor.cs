using System.Text.Json;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Statements.Analysis;
using FinanceFoodTracker.Domain.Entities;
using FinanceFoodTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceFoodTracker.Application.Statements;

public sealed class StatementProcessor : IStatementProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IApplicationDbContext _db;
    private readonly IStatementAnalyzer _analyzer;
    private readonly ILogger<StatementProcessor> _logger;

    public StatementProcessor(IApplicationDbContext db, IStatementAnalyzer analyzer, ILogger<StatementProcessor> logger)
    {
        _db = db;
        _analyzer = analyzer;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid statementId, CancellationToken cancellationToken = default)
    {
        var statement = await _db.Statements.FirstOrDefaultAsync(s => s.Id == statementId, cancellationToken);

        if (statement is null || statement.Status != ProcessingStatus.Pending)
        {
            return;
        }

        try
        {
            var categories = await _db.Categories
                .Where(c => c.UserId == null || c.UserId == statement.UserId)
                .ToListAsync(cancellationToken);

            var expense = categories.Where(c => c.Type == TransactionType.Expense).Select(c => c.Name).ToList();
            var income = categories.Where(c => c.Type == TransactionType.Income).Select(c => c.Name).ToList();

            var result = await _analyzer.AnalyzeAsync(
                new StatementAnalysisRequest(statement.RawText, expense, income, statement.Id.ToString()),
                cancellationToken);

            statement.AiRawResponse = EnsureJson(result.RawResponse);

            var operations = result.Operations.Select(operation =>
            {
                var category = FindCategory(operation.CategoryHint, operation.Direction, categories);

                return new StatementOperationDto(
                    operation.OccurredAt,
                    operation.Amount,
                    operation.Direction,
                    operation.Description,
                    operation.Place,
                    operation.Currency,
                    operation.Mcc,
                    operation.IsTransfer,
                    category?.Id,
                    category?.Name,
                    operation.CategoryHint,
                    operation.Confidence);
            }).ToList();

            statement.ParsedOperations = JsonSerializer.Serialize(operations, JsonOptions);
            statement.Status = operations.Count > 0 ? ProcessingStatus.Processed : ProcessingStatus.NeedsReview;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "Не удалось разобрать выписку {StatementId}", statementId);
            statement.Status = ProcessingStatus.Failed;
            statement.Error = exception.Message.Length > 900 ? exception.Message[..900] : exception.Message;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static Category? FindCategory(string? hint, string direction, IReadOnlyList<Category> categories)
    {
        if (string.IsNullOrWhiteSpace(hint))
        {
            return null;
        }

        var type = direction == "income" ? TransactionType.Income : TransactionType.Expense;
        var pool = categories.Where(c => c.Type == type).ToList();
        var normalized = hint.Trim();

        var exact = pool.FirstOrDefault(c => string.Equals(c.Name, normalized, StringComparison.OrdinalIgnoreCase));

        if (exact is not null)
        {
            return exact;
        }

        return pool.FirstOrDefault(c =>
            c.Name.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(c.Name, StringComparison.OrdinalIgnoreCase));
    }

    private static string? EnsureJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            using var _ = JsonDocument.Parse(raw);
            return raw;
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(new { raw });
        }
    }
}
