using FinanceFoodTracker.Application.Common.Exceptions;
using FinanceFoodTracker.Application.Common.Images;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Domain.Entities;
using FinanceFoodTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceFoodTracker.Application.FoodLogs;

public sealed class FoodLogService : IFoodLogService
{
    private readonly IApplicationDbContext _db;
    private readonly IFileStorage _fileStorage;
    private readonly IFoodLogProcessingQueue _queue;

    public FoodLogService(IApplicationDbContext db, IFileStorage fileStorage, IFoodLogProcessingQueue queue)
    {
        _db = db;
        _fileStorage = fileStorage;
        _queue = queue;
    }

    public async Task<FoodLogDto> CreateAsync(Guid userId, byte[] content, string fileName, string? context = null, CancellationToken cancellationToken = default, long? telegramChatId = null)
    {
        if (content.Length == 0)
        {
            throw new ValidationException("Файл пуст.");
        }

        var detected = ImageContent.Detect(content)
            ?? throw new ValidationException("Поддерживаются только изображения JPEG, PNG и WebP.");

        var imagePath = await _fileStorage.SaveAsync(content, detected.Extension, cancellationToken);

        var log = new FoodLog
        {
            UserId = userId,
            ImagePath = imagePath,
            EatenAt = DateTimeOffset.UtcNow,
            Status = ProcessingStatus.Pending,
            UserContext = Normalize(context),
            TelegramChatId = telegramChatId
        };

        _db.FoodLogs.Add(log);
        await _db.SaveChangesAsync(cancellationToken);

        await _queue.EnqueueAsync(log.Id, cancellationToken);

        return FoodLogMapper.ToDto(log);
    }

    public async Task<FoodLogDto> CreateFromTextAsync(Guid userId, string text, CancellationToken cancellationToken = default, long? telegramChatId = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ValidationException("Описание блюда пустое.");
        }

        var log = new FoodLog
        {
            UserId = userId,
            ImagePath = string.Empty,
            UserContext = text.Trim(),
            EatenAt = DateTimeOffset.UtcNow,
            Status = ProcessingStatus.Pending,
            TelegramChatId = telegramChatId
        };

        _db.FoodLogs.Add(log);
        await _db.SaveChangesAsync(cancellationToken);

        await _queue.EnqueueAsync(log.Id, cancellationToken);

        return FoodLogMapper.ToDto(log);
    }

    public async Task<IReadOnlyList<FoodLogDto>> GetAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var fromUtc = from.ToUniversalTime();
        var toUtc = to.ToUniversalTime();

        return await _db.FoodLogs
            .AsNoTracking()
            .Where(f => f.UserId == userId && f.EatenAt >= fromUtc && f.EatenAt <= toUtc)
            .OrderByDescending(f => f.EatenAt)
            .Select(f => FoodLogMapper.ToDto(f))
            .ToListAsync(cancellationToken);
    }

    public async Task<FoodLogDto> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var log = await GetAsync(userId, id, cancellationToken);
        return FoodLogMapper.ToDto(log);
    }

    public async Task<FoodLogImageDto> GetImageAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var log = await _db.FoodLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Блюдо не найдено.");

        if (!_fileStorage.Exists(log.ImagePath))
        {
            throw new NotFoundException("Файл блюда не найден.");
        }

        var content = await _fileStorage.ReadAsync(log.ImagePath, cancellationToken);
        return new FoodLogImageDto(content, ImageContent.ContentTypeFor(log.ImagePath));
    }

    public async Task<FoodLogDto> UpdateAsync(Guid userId, Guid id, UpdateFoodLogRequest request, CancellationToken cancellationToken = default)
    {
        var log = await GetAsync(userId, id, cancellationToken);

        if (request.DishName is not null)
        {
            log.DishName = string.IsNullOrWhiteSpace(request.DishName) ? null : request.DishName.Trim();
        }

        if (request.UserContext is not null)
        {
            log.UserContext = Normalize(request.UserContext);
        }

        log.CaloriesMin = request.CaloriesMin ?? log.CaloriesMin;
        log.CaloriesMax = request.CaloriesMax ?? log.CaloriesMax;
        log.ProteinMinG = request.ProteinMinG ?? log.ProteinMinG;
        log.ProteinMaxG = request.ProteinMaxG ?? log.ProteinMaxG;
        log.FatMinG = request.FatMinG ?? log.FatMinG;
        log.FatMaxG = request.FatMaxG ?? log.FatMaxG;
        log.CarbsMinG = request.CarbsMinG ?? log.CarbsMinG;
        log.CarbsMaxG = request.CarbsMaxG ?? log.CarbsMaxG;

        EnsureNonNegative(log);

        log.ProteinG = FoodLogProcessor.Midpoint(log.ProteinMinG, log.ProteinMaxG);
        log.FatG = FoodLogProcessor.Midpoint(log.FatMinG, log.FatMaxG);
        log.CarbsG = FoodLogProcessor.Midpoint(log.CarbsMinG, log.CarbsMaxG);

        if (request.EatenAt is not null)
        {
            log.EatenAt = request.EatenAt.Value.ToUniversalTime();
        }

        await _db.SaveChangesAsync(cancellationToken);

        return FoodLogMapper.ToDto(log);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var log = await GetAsync(userId, id, cancellationToken);

        _db.FoodLogs.Remove(log);
        await _db.SaveChangesAsync(cancellationToken);

        if (_fileStorage.Exists(log.ImagePath))
        {
            await _fileStorage.DeleteAsync(log.ImagePath, cancellationToken);
        }
    }

    public async Task<FoodLogDto> ReanalyzeAsync(Guid userId, Guid id, string? context, CancellationToken cancellationToken = default)
    {
        var log = await GetAsync(userId, id, cancellationToken);

        if (context is not null)
        {
            log.UserContext = Normalize(context);
        }

        log.Status = ProcessingStatus.Pending;
        await _db.SaveChangesAsync(cancellationToken);
        await _queue.EnqueueAsync(log.Id, cancellationToken);

        return FoodLogMapper.ToDto(log);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<FoodLog> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken)
        => await _db.FoodLogs
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Блюдо не найдено.");

    private static void EnsureNonNegative(FoodLog log)
    {
        var values = new[]
        {
            log.CaloriesMin, log.CaloriesMax,
            log.ProteinMinG, log.ProteinMaxG,
            log.FatMinG, log.FatMaxG,
            log.CarbsMinG, log.CarbsMaxG
        };

        if (values.Any(value => value is < 0))
        {
            throw new ValidationException("Значения не могут быть отрицательными.");
        }
    }

}
