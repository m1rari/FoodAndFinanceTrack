using FinanceFoodTracker.Application.Common.Exceptions;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Receipts.Analysis;
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
    private readonly IReceiptAnalyzer _analyzer;

    public ReceiptService(IApplicationDbContext db, IFileStorage fileStorage, IReceiptAnalyzer analyzer)
    {
        _db = db;
        _fileStorage = fileStorage;
        _analyzer = analyzer;
    }

    public async Task<ReceiptDto> CreateAsync(Guid userId, byte[] content, string fileName, CancellationToken cancellationToken = default)
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
            Status = ProcessingStatus.Pending
        };

        _db.Receipts.Add(receipt);
        await _db.SaveChangesAsync(cancellationToken);

        await AnalyzeAsync(receipt, imagePath, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);


        return await GetDtoAsync(userId, receipt.Id, cancellationToken);
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

    public async Task<ReceiptDto> AddItemAsync(Guid userId, Guid receiptId, CreateReceiptItemRequest request, CancellationToken cancellationToken = default)
    {
        var receipt = await GetReceiptAsync(userId, receiptId, cancellationToken);

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

    private async Task AnalyzeAsync(Receipt receipt, string imagePath, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _analyzer.AnalyzeAsync(imagePath, cancellationToken);

            receipt.MerchantName = result.MerchantName;
            receipt.PurchaseDate = result.PurchaseDate;
            receipt.TotalAmount = result.TotalAmount;
            receipt.RawOcrText = result.RawOcrText;
            receipt.AiRawResponse = result.RawResponse;
            receipt.Confidence = result.Confidence;

            foreach (var item in result.SafeItems)
            {
                var entity = new ReceiptItem
                {
                    ReceiptId = receipt.Id,
                    Name = item.Name,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalPrice = item.TotalPrice,
                    Confidence = item.Confidence
                };

                _db.ReceiptItems.Add(entity);
            }

            receipt.Status = result.SafeItems.Count > 0 ? ProcessingStatus.Processed : ProcessingStatus.NeedsReview;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            receipt.Status = ProcessingStatus.Failed;
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
                    .ToList()))
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
