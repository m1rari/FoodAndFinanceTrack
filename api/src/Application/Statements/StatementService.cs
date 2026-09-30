using System.Security.Cryptography;
using System.Text.Json;
using FinanceFoodTracker.Application.Common.Exceptions;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Transactions;
using FinanceFoodTracker.Domain.Entities;
using FinanceFoodTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceFoodTracker.Application.Statements;

public sealed class StatementService : IStatementService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IApplicationDbContext _db;
    private readonly IFileStorage _fileStorage;
    private readonly IPdfTextExtractor _pdf;
    private readonly IStatementProcessingQueue _queue;

    public StatementService(
        IApplicationDbContext db,
        IFileStorage fileStorage,
        IPdfTextExtractor pdf,
        IStatementProcessingQueue queue)
    {
        _db = db;
        _fileStorage = fileStorage;
        _pdf = pdf;
        _queue = queue;
    }

    public async Task<StatementDto> CreateAsync(Guid userId, byte[] pdf, string fileName, CancellationToken cancellationToken = default)
    {
        if (pdf.Length == 0)
        {
            throw new ValidationException("Файл пуст.");
        }

        if (!LooksLikePdf(pdf))
        {
            throw new ValidationException("Ожидается PDF-файл выписки.");
        }

        var contentHash = ComputeHash(pdf);

        var existing = await _db.Statements
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.ContentHash == contentHash, cancellationToken);

        if (existing is not null)
        {
            return ToDto(existing, duplicate: true);
        }

        var path = await _fileStorage.SaveAsync(pdf, ".pdf", cancellationToken);
        var text = _pdf.Extract(pdf);

        var statement = new Statement
        {
            UserId = userId,
            FileName = string.IsNullOrWhiteSpace(fileName) ? "statement.pdf" : fileName,
            PdfPath = path,
            RawText = text,
            ContentHash = contentHash,
            Status = ProcessingStatus.Pending
        };

        _db.Statements.Add(statement);
        await _db.SaveChangesAsync(cancellationToken);

        await _queue.EnqueueAsync(statement.Id, cancellationToken);

        return ToDto(statement);
    }

    public async Task<StatementDto> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var statement = await GetEntityAsync(userId, id, cancellationToken);
        return ToDto(statement);
    }

    public async Task<IReadOnlyList<StatementMatchDto>> GetMatchesAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var statement = await GetEntityAsync(userId, id, cancellationToken);
        var operations = DeserializeOperations(statement);
        var matches = new List<StatementMatchDto>();
        const decimal tolerance = 0.01m;

        for (var index = 0; index < operations.Count; index++)
        {
            var operation = operations[index];
            var type = operation.Direction == "income" ? TransactionType.Income : TransactionType.Expense;
            var anchor = operation.OccurredAt.ToUniversalTime();
            var from = anchor.AddDays(-1);
            var to = anchor.AddDays(1);
            var amount = operation.Amount;

            var candidates = await _db.Transactions
                .Include(t => t.Category)
                .Include(t => t.Receipt)
                .Where(t => t.UserId == userId
                    && t.Type == type
                    && t.IsTransfer == operation.IsTransfer
                    && t.Amount >= amount - tolerance
                    && t.Amount <= amount + tolerance
                    && t.OccurredAt >= from
                    && t.OccurredAt <= to)
                .OrderBy(t => t.OccurredAt)
                .Take(5)
                .Select(t => TransactionMapper.ToDto(t))
                .ToListAsync(cancellationToken);

            matches.Add(new StatementMatchDto(index, candidates));
        }

        return matches;
    }

    public async Task<StatementDto> ConfirmAsync(Guid userId, Guid id, ConfirmStatementRequest request, CancellationToken cancellationToken = default)
    {
        var statement = await GetEntityAsync(userId, id, cancellationToken);

        if (statement.ConfirmedAt is not null)
        {
            throw new ValidationException("Выписка уже проведена.");
        }

        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("У пользователя нет счёта.");

        var operations = request.Operations ?? Array.Empty<StatementOperationDto>();
        var createdCount = 0;

        foreach (var operation in operations)
        {
            if (operation.Amount <= 0)
            {
                continue;
            }

            if (operation.LinkTransactionId is Guid linkId)
            {
                var existing = await _db.Transactions
                    .FirstOrDefaultAsync(t => t.Id == linkId && t.UserId == userId, cancellationToken);

                if (existing is not null)
                {
                    if (existing.CategoryId is null && operation.CategoryId is not null)
                    {
                        existing.CategoryId = operation.CategoryId;
                    }

                    if (string.IsNullOrWhiteSpace(existing.Comment) && !string.IsNullOrWhiteSpace(operation.Description))
                    {
                        existing.Comment = operation.Description;
                    }

                    continue;
                }
            }

            _db.Transactions.Add(new Transaction
            {
                UserId = userId,
                AccountId = account.Id,
                CategoryId = operation.CategoryId,
                Type = operation.Direction == "income" ? TransactionType.Income : TransactionType.Expense,
                Amount = operation.Amount,
                Currency = string.IsNullOrWhiteSpace(operation.Currency) ? account.Currency : operation.Currency,
                OccurredAt = operation.OccurredAt.ToUniversalTime(),
                Source = TransactionSource.Statement,
                Comment = string.IsNullOrWhiteSpace(operation.Description) ? operation.Place : operation.Description,
                IsTransfer = operation.IsTransfer
            });

            createdCount++;
        }

        statement.ConfirmedAt = DateTimeOffset.UtcNow;
        statement.CreatedCount = createdCount;
        statement.ParsedOperations = JsonSerializer.Serialize(operations, JsonOptions);

        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(statement);
    }

    private async Task<Statement> GetEntityAsync(Guid userId, Guid id, CancellationToken cancellationToken)
        => await _db.Statements
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Выписка не найдена.");

    private static List<StatementOperationDto> DeserializeOperations(Statement statement)
        => string.IsNullOrWhiteSpace(statement.ParsedOperations)
            ? new List<StatementOperationDto>()
            : JsonSerializer.Deserialize<List<StatementOperationDto>>(statement.ParsedOperations, JsonOptions) ?? new();

    private static StatementDto ToDto(Statement statement, bool duplicate = false)
    {
        var operations = DeserializeOperations(statement);

        return new StatementDto(
            statement.Id,
            statement.FileName,
            statement.Status.ToString(),
            statement.ConfirmedAt is not null,
            statement.CreatedCount,
            statement.CreatedAt,
            statement.Error,
            operations,
            duplicate);
    }

    private static string ComputeHash(byte[] content)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(content));
    }

    private static bool LooksLikePdf(byte[] content)
        => content.Length >= 4 && content[0] == (byte)'%' && content[1] == (byte)'P' && content[2] == (byte)'D' && content[3] == (byte)'F';
}
