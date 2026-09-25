using FinanceFoodTracker.Application.Common.Exceptions;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Transactions;
using FinanceFoodTracker.Domain.Entities;
using FinanceFoodTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceFoodTracker.Application.Receipts;

public sealed class ReceiptService : IReceiptService
{
    private const string Jpeg = "image/jpeg";
    private const string Png = "image/png";
    private const string Webp = "image/webp";

    private readonly IApplicationDbContext _db;
    private readonly IFileStorage _fileStorage;
    private readonly IReceiptProcessingQueue _queue;

    public ReceiptService(IApplicationDbContext db, IFileStorage fileStorage, IReceiptProcessingQueue queue)
    {
        _db = db;
        _fileStorage = fileStorage;
        _queue = queue;
    }

    public async Task<ReceiptDto> CreateAsync(Guid userId, byte[] content, string fileName, CancellationToken cancellationToken = default, long? telegramChatId = null)
    {
        if (content.Length == 0)
        {
            throw new ValidationException("Файл пуст.");
        }

        var detected = DetectImage(content)
            ?? throw new ValidationException("Поддерживаются только изображения JPEG, PNG и WebP.");

        var imagePath = await _fileStorage.SaveAsync(content, detected.Extension, cancellationToken);

        var receipt = new Receipt
        {
            UserId = userId,
            ImagePath = imagePath,
            Status = ProcessingStatus.Pending,
            TelegramChatId = telegramChatId
        };

        _db.Receipts.Add(receipt);
        await _db.SaveChangesAsync(cancellationToken);

        await _queue.EnqueueAsync(receipt.Id, cancellationToken);

        return await GetDtoAsync(userId, receipt.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<ReceiptSummaryDto>> GetListAsync(Guid userId, bool onlyUnconfirmed = false, CancellationToken cancellationToken = default)
    {
        var query = _db.Receipts
            .AsNoTracking()
            .Where(r => r.UserId == userId);

        if (onlyUnconfirmed)
        {
            query = query.Where(r => !_db.Transactions.Any(t => t.ReceiptId == r.Id));
        }

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .Select(r => new ReceiptSummaryDto(
                r.Id,
                r.MerchantName,
                r.PurchaseDate,
                r.TotalAmount,
                r.Status.ToString(),
                r.Items.Count,
                _db.Transactions.Any(t => t.ReceiptId == r.Id),
                r.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<ReceiptDto> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
        => await GetDtoAsync(userId, id, cancellationToken);

    public async Task<ReceiptImageDto> GetImageAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var receipt = await _db.Receipts
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Чек не найден.");

        if (!_fileStorage.Exists(receipt.ImagePath))
        {
            throw new NotFoundException("Файл чека не найден.");
        }

        var content = await _fileStorage.ReadAsync(receipt.ImagePath, cancellationToken);
        return new ReceiptImageDto(content, ContentTypeFor(receipt.ImagePath));
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var receipt = await _db.Receipts
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Чек не найден.");

        var transactions = await _db.Transactions
            .Where(t => t.ReceiptId == receipt.Id)
            .ToListAsync(cancellationToken);

        foreach (var transaction in transactions)
        {
            if (transaction.Source == TransactionSource.Receipt)
            {
                _db.Transactions.Remove(transaction);
            }
            else
            {
                transaction.ReceiptId = null;
            }
        }

        _db.ReceiptItems.RemoveRange(receipt.Items);
        _db.Receipts.Remove(receipt);
        await _db.SaveChangesAsync(cancellationToken);

        if (_fileStorage.Exists(receipt.ImagePath))
        {
            await _fileStorage.DeleteAsync(receipt.ImagePath, cancellationToken);
        }
    }

    public async Task<ReceiptDto> AddItemAsync(Guid userId, Guid receiptId, CreateReceiptItemRequest request, CancellationToken cancellationToken = default)
    {
        var receipt = await GetReceiptAsync(userId, receiptId, cancellationToken);
        await EnsureNotConfirmedAsync(receiptId, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Название позиции обязательно.");
        }

        if (request.Quantity <= 0)
        {
            throw new ValidationException("Количество должно быть больше нуля.");
        }

        if (request.UnitPrice < 0)
        {
            throw new ValidationException("Цена не может быть отрицательной.");
        }

        if (request.CategoryId is not null)
        {
            await EnsureCategoryAsync(userId, request.CategoryId.Value, cancellationToken);
        }

        var total = request.TotalPrice ?? request.Quantity * request.UnitPrice;

        if (total < 0)
        {
            throw new ValidationException("Сумма не может быть отрицательной.");
        }

        var item = new ReceiptItem
        {
            ReceiptId = receipt.Id,
            Name = request.Name.Trim(),
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            TotalPrice = total,
            CategoryId = request.CategoryId
        };

        _db.ReceiptItems.Add(item);

        await RefreshAsync(receipt, cancellationToken);
        return await GetDtoAsync(userId, receipt.Id, cancellationToken);
    }

    public async Task<ReceiptDto> UpdateItemAsync(Guid userId, Guid receiptId, Guid itemId, UpdateReceiptItemRequest request, CancellationToken cancellationToken = default)
    {
        var receipt = await GetReceiptAsync(userId, receiptId, cancellationToken);
        await EnsureNotConfirmedAsync(receiptId, cancellationToken);

        var item = receipt.Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new NotFoundException("Позиция не найдена.");

        if (request.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ValidationException("Название позиции обязательно.");
            }

            item.Name = request.Name.Trim();
        }

        if (request.Quantity is not null)
        {
            if (request.Quantity <= 0)
            {
                throw new ValidationException("Количество должно быть больше нуля.");
            }

            item.Quantity = request.Quantity.Value;
        }

        if (request.UnitPrice is not null)
        {
            if (request.UnitPrice < 0)
            {
                throw new ValidationException("Цена не может быть отрицательной.");
            }

            item.UnitPrice = request.UnitPrice.Value;
        }

        if (request.ClearCategory)
        {
            item.CategoryId = null;
        }
        else if (request.CategoryId is not null)
        {
            await EnsureCategoryAsync(userId, request.CategoryId.Value, cancellationToken);
            item.CategoryId = request.CategoryId;
        }

        item.TotalPrice = request.TotalPrice ?? item.Quantity * item.UnitPrice;

        if (item.TotalPrice < 0)
        {
            throw new ValidationException("Сумма не может быть отрицательной.");
        }

        await RefreshAsync(receipt, cancellationToken);
        return await GetDtoAsync(userId, receipt.Id, cancellationToken);
    }

    public async Task<ReceiptDto> ConfirmAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var receipt = await GetReceiptAsync(userId, id, cancellationToken);

        if (receipt.Items.Count == 0)
        {
            throw new ValidationException("Нет позиций для проведения.");
        }

        if (await _db.Transactions.AnyAsync(t => t.ReceiptId == receipt.Id, cancellationToken))
        {
            throw new ValidationException("Чек уже проведён в операции.");
        }

        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("У пользователя нет счёта.");

        var occurredAt = receipt.PurchaseDate ?? receipt.CreatedAt;

        foreach (var item in receipt.Items)
        {
            _db.Transactions.Add(new Transaction
            {
                UserId = userId,
                AccountId = account.Id,
                CategoryId = item.CategoryId,
                Type = TransactionType.Expense,
                Amount = item.TotalPrice,
                Currency = account.Currency,
                OccurredAt = occurredAt,
                Source = TransactionSource.Receipt,
                Comment = item.Name,
                ReceiptId = receipt.Id
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await GetDtoAsync(userId, receipt.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<TransactionDto>> GetMatchesAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var receipt = await GetReceiptAsync(userId, id, cancellationToken);

        if (await _db.Transactions.AnyAsync(t => t.ReceiptId == receipt.Id, cancellationToken))
        {
            return Array.Empty<TransactionDto>();
        }

        var total = receipt.TotalAmount ?? receipt.Items.Sum(i => i.TotalPrice);

        if (total <= 0)
        {
            return Array.Empty<TransactionDto>();
        }

        var anchor = receipt.PurchaseDate ?? receipt.CreatedAt;
        var from = anchor.AddDays(-1);
        var to = anchor.AddDays(1);
        const decimal tolerance = 0.01m;

        return await _db.Transactions
            .Include(t => t.Category)
            .Include(t => t.Receipt)
            .Where(t => t.UserId == userId
                && t.Type == TransactionType.Expense
                && t.Source == TransactionSource.Manual
                && t.ReceiptId == null
                && t.OccurredAt >= from
                && t.OccurredAt <= to
                && t.Amount >= total - tolerance
                && t.Amount <= total + tolerance)
            .OrderByDescending(t => t.OccurredAt)
            .Select(t => TransactionMapper.ToDto(t))
            .ToListAsync(cancellationToken);
    }

    public async Task<ReceiptDto> LinkAsync(Guid userId, Guid id, Guid transactionId, CancellationToken cancellationToken = default)
    {
        var receipt = await GetReceiptAsync(userId, id, cancellationToken);

        if (await _db.Transactions.AnyAsync(t => t.ReceiptId == receipt.Id, cancellationToken))
        {
            throw new ValidationException("Чек уже проведён в операции.");
        }

        var transaction = await _db.Transactions
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.UserId == userId && t.ReceiptId == null, cancellationToken)
            ?? throw new NotFoundException("Операция не найдена.");

        transaction.ReceiptId = receipt.Id;

        if (string.IsNullOrWhiteSpace(transaction.Comment) && !string.IsNullOrWhiteSpace(receipt.MerchantName))
        {
            transaction.Comment = receipt.MerchantName;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await GetDtoAsync(userId, receipt.Id, cancellationToken);
    }

    private async Task EnsureNotConfirmedAsync(Guid receiptId, CancellationToken cancellationToken)
    {
        if (await _db.Transactions.AnyAsync(t => t.ReceiptId == receiptId, cancellationToken))
        {
            throw new ValidationException("Чек уже проведён, правки запрещены.");
        }
    }

    private async Task RefreshAsync(Receipt receipt, CancellationToken cancellationToken)
    {
        await _db.SaveChangesAsync(cancellationToken);

        var total = await _db.ReceiptItems
            .Where(i => i.ReceiptId == receipt.Id)
            .SumAsync(i => (decimal?)i.TotalPrice, cancellationToken) ?? 0m;

        var count = await _db.ReceiptItems
            .CountAsync(i => i.ReceiptId == receipt.Id, cancellationToken);

        receipt.TotalAmount = total;
        receipt.Status = count > 0 ? ProcessingStatus.Processed : ProcessingStatus.NeedsReview;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Receipt> GetReceiptAsync(Guid userId, Guid id, CancellationToken cancellationToken)
        => await _db.Receipts
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Чек не найден.");

    private async Task<ReceiptDto> GetDtoAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var dto = await _db.Receipts
            .AsNoTracking()
            .Where(r => r.Id == id && r.UserId == userId)
            .Select(r => new ReceiptDto(
                r.Id,
                r.MerchantName,
                r.PurchaseDate,
                r.TotalAmount,
                r.Status.ToString(),
                r.Confidence,
                r.CreatedAt,
                $"/api/receipts/{r.Id}/image",
                r.Items
                    .OrderBy(i => i.Name)
                    .Select(i => new ReceiptItemDto(
                        i.Id,
                        i.Name,
                        i.Quantity,
                        i.UnitPrice,
                        i.TotalPrice,
                        i.CategoryId,
                        i.Category != null ? i.Category.Name : null,
                        i.Confidence))
                    .ToList(),
                _db.Transactions.Any(t => t.ReceiptId == r.Id)))
            .FirstOrDefaultAsync(cancellationToken);

        return dto ?? throw new NotFoundException("Чек не найден.");
    }

    private async Task EnsureCategoryAsync(Guid userId, Guid categoryId, CancellationToken cancellationToken)
    {
        var category = await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && (c.UserId == null || c.UserId == userId), cancellationToken)
            ?? throw new ValidationException("Категория не найдена.");

        if (category.Type != TransactionType.Expense)
        {
            throw new ValidationException("Позиции чека используют только категории расходов.");
        }
    }

    private static (string ContentType, string Extension)? DetectImage(byte[] content)
    {
        if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
        {
            return (Jpeg, ".jpg");
        }

        if (content.Length >= 8
            && content[0] == 0x89 && content[1] == 0x50 && content[2] == 0x4E && content[3] == 0x47
            && content[4] == 0x0D && content[5] == 0x0A && content[6] == 0x1A && content[7] == 0x0A)
        {
            return (Png, ".png");
        }

        if (content.Length >= 12
            && content[0] == (byte)'R' && content[1] == (byte)'I' && content[2] == (byte)'F' && content[3] == (byte)'F'
            && content[8] == (byte)'W' && content[9] == (byte)'E' && content[10] == (byte)'B' && content[11] == (byte)'P')
        {
            return (Webp, ".webp");
        }

        return null;
    }

    private static string ContentTypeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => Png,
        ".webp" => Webp,
        _ => Jpeg
    };
}
