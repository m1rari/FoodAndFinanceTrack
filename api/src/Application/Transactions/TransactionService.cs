using FinanceFoodTracker.Application.Common.Exceptions;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Domain.Entities;
using FinanceFoodTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceFoodTracker.Application.Transactions;

public sealed class TransactionService : ITransactionService
{
    private readonly IApplicationDbContext _db;

    public TransactionService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TransactionDto>> GetAsync(Guid userId, TransactionFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _db.Transactions
            .Include(t => t.Category)
            .Where(t => t.UserId == userId);

        if (filter.From is not null)
        {
            query = query.Where(t => t.OccurredAt >= filter.From);
        }

        if (filter.To is not null)
        {
            query = query.Where(t => t.OccurredAt <= filter.To);
        }

        if (filter.CategoryId is not null)
        {
            query = query.Where(t => t.CategoryId == filter.CategoryId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Type))
        {
            if (!Enum.TryParse<TransactionType>(filter.Type, ignoreCase: true, out var type))
            {
                throw new ValidationException($"Неизвестный тип операции: {filter.Type}");
            }

            query = query.Where(t => t.Type == type);
        }

        return await query
            .OrderByDescending(t => t.OccurredAt)
            .Select(t => ToDto(t))
            .ToListAsync(cancellationToken);
    }

    public async Task<TransactionDto> CreateAsync(Guid userId, CreateTransactionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
        {
            throw new ValidationException("Сумма должна быть больше нуля.");
        }

        if (!Enum.TryParse<TransactionType>(request.Type, ignoreCase: true, out var type))
        {
            throw new ValidationException($"Неизвестный тип операции: {request.Type}");
        }

        var account = await ResolveAccountAsync(userId, request.AccountId, cancellationToken);

        if (request.CategoryId is not null)
        {
            await EnsureCategoryAsync(userId, request.CategoryId.Value, type, cancellationToken);
        }

        var transaction = new Transaction
        {
            UserId = userId,
            AccountId = account.Id,
            CategoryId = request.CategoryId,
            Type = type,
            Amount = request.Amount,
            Currency = account.Currency,
            OccurredAt = request.OccurredAt ?? DateTimeOffset.UtcNow,
            Source = TransactionSource.Manual,
            Comment = request.Comment
        };

        _db.Transactions.Add(transaction);
        await _db.SaveChangesAsync(cancellationToken);

        return await GetDtoAsync(userId, transaction.Id, cancellationToken);
    }

    public async Task<TransactionDto> UpdateAsync(Guid userId, Guid id, UpdateTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var transaction = await _db.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Операция не найдена.");

        if (request.Amount is not null)
        {
            if (request.Amount <= 0)
            {
                throw new ValidationException("Сумма должна быть больше нуля.");
            }

            transaction.Amount = request.Amount.Value;
        }

        if (request.CategoryId is not null)
        {
            await EnsureCategoryAsync(userId, request.CategoryId.Value, transaction.Type, cancellationToken);
            transaction.CategoryId = request.CategoryId;
        }

        if (request.OccurredAt is not null)
        {
            transaction.OccurredAt = request.OccurredAt.Value;
        }

        if (request.Comment is not null)
        {
            transaction.Comment = request.Comment;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await GetDtoAsync(userId, transaction.Id, cancellationToken);
    }

    private async Task<Account> ResolveAccountAsync(Guid userId, Guid? accountId, CancellationToken cancellationToken)
    {
        if (accountId is not null)
        {
            return await _db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId, cancellationToken)
                ?? throw new NotFoundException("Счёт не найден.");
        }

        return await _db.Accounts.FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("У пользователя нет счёта.");
    }

    private async Task EnsureCategoryAsync(Guid userId, Guid categoryId, TransactionType type, CancellationToken cancellationToken)
    {
        var category = await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && (c.UserId == null || c.UserId == userId), cancellationToken)
            ?? throw new ValidationException("Категория не найдена.");

        if (category.Type != type)
        {
            throw new ValidationException("Тип категории не соответствует типу операции.");
        }
    }

    private async Task<TransactionDto> GetDtoAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        return await _db.Transactions
            .Include(t => t.Category)
            .Where(t => t.Id == id && t.UserId == userId)
            .Select(t => ToDto(t))
            .FirstAsync(cancellationToken);
    }

    private static TransactionDto ToDto(Transaction t) => new(
        t.Id,
        t.AccountId,
        t.CategoryId,
        t.Category != null ? t.Category.Name : null,
        t.Type.ToString(),
        t.Amount,
        t.Currency,
        t.OccurredAt,
        t.Source.ToString(),
        t.Comment,
        t.ReceiptId,
        t.CreatedAt);
}
