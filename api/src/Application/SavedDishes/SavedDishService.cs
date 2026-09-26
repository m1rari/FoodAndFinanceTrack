using FinanceFoodTracker.Application.Common.Exceptions;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.FoodLogs;
using FinanceFoodTracker.Domain.Entities;
using FinanceFoodTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceFoodTracker.Application.SavedDishes;

public sealed class SavedDishService : ISavedDishService
{
    private readonly IApplicationDbContext _db;

    public SavedDishService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SavedDishDto>> GetAsync(Guid userId, bool? favorite, int limit, CancellationToken cancellationToken = default)
    {
        var query = _db.SavedDishes.Where(d => d.UserId == userId);

        if (favorite is not null)
        {
            query = query.Where(d => d.IsFavorite == favorite);
        }

        return await query
            .OrderByDescending(d => d.LastUsedAt)
            .Take(limit <= 0 ? 20 : limit)
            .Select(d => ToDto(d))
            .ToListAsync(cancellationToken);
    }

    public async Task<FoodLogDto> AddToDiaryAsync(Guid userId, Guid savedDishId, CancellationToken cancellationToken = default)
    {
        var dish = await _db.SavedDishes
            .FirstOrDefaultAsync(d => d.Id == savedDishId && d.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Блюдо не найдено.");

        var log = new FoodLog
        {
            UserId = userId,
            ImagePath = string.Empty,
            DishName = dish.Name,
            UserContext = dish.Name,
            CaloriesMin = dish.CaloriesMin,
            CaloriesMax = dish.CaloriesMax,
            ProteinMinG = dish.ProteinMinG,
            ProteinMaxG = dish.ProteinMaxG,
            FatMinG = dish.FatMinG,
            FatMaxG = dish.FatMaxG,
            CarbsMinG = dish.CarbsMinG,
            CarbsMaxG = dish.CarbsMaxG,
            ProteinG = FoodLogProcessor.Midpoint(dish.ProteinMinG, dish.ProteinMaxG),
            FatG = FoodLogProcessor.Midpoint(dish.FatMinG, dish.FatMaxG),
            CarbsG = FoodLogProcessor.Midpoint(dish.CarbsMinG, dish.CarbsMaxG),
            EatenAt = DateTimeOffset.UtcNow,
            Status = ProcessingStatus.Processed
        };

        dish.UseCount += 1;
        dish.LastUsedAt = DateTimeOffset.UtcNow;

        _db.FoodLogs.Add(log);
        await _db.SaveChangesAsync(cancellationToken);

        return FoodLogMapper.ToDto(log);
    }

    public async Task<SavedDishDto> SetFavoriteAsync(Guid userId, Guid savedDishId, bool isFavorite, CancellationToken cancellationToken = default)
    {
        var dish = await _db.SavedDishes
            .FirstOrDefaultAsync(d => d.Id == savedDishId && d.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Блюдо не найдено.");

        dish.IsFavorite = isFavorite;
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(dish);
    }

    public async Task DeleteAsync(Guid userId, Guid savedDishId, CancellationToken cancellationToken = default)
    {
        var dish = await _db.SavedDishes
            .FirstOrDefaultAsync(d => d.Id == savedDishId && d.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Блюдо не найдено.");

        _db.SavedDishes.Remove(dish);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SavedDishDto?> UpsertFromFoodLogAsync(FoodLog log, bool? favorite, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(log.DishName)
            || string.Equals(log.DishName, "Не определено", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var name = log.DishName.Trim();
        var key = name.ToLowerInvariant();

        var dish = await _db.SavedDishes
            .FirstOrDefaultAsync(d => d.UserId == log.UserId && d.NameKey == key, cancellationToken);

        if (dish is null)
        {
            dish = new SavedDish
            {
                UserId = log.UserId,
                Name = name,
                NameKey = key,
                UseCount = 1,
                LastUsedAt = DateTimeOffset.UtcNow
            };
            _db.SavedDishes.Add(dish);
        }
        else
        {
            dish.Name = name;
            dish.UseCount += 1;
            dish.LastUsedAt = DateTimeOffset.UtcNow;
        }

        dish.CaloriesMin = log.CaloriesMin;
        dish.CaloriesMax = log.CaloriesMax;
        dish.ProteinMinG = log.ProteinMinG;
        dish.ProteinMaxG = log.ProteinMaxG;
        dish.FatMinG = log.FatMinG;
        dish.FatMaxG = log.FatMaxG;
        dish.CarbsMinG = log.CarbsMinG;
        dish.CarbsMaxG = log.CarbsMaxG;

        if (favorite is not null)
        {
            dish.IsFavorite = favorite.Value;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(dish);
    }

    public async Task SetFavoriteFromFoodLogAsync(Guid userId, Guid foodLogId, bool isFavorite, CancellationToken cancellationToken = default)
    {
        var log = await _db.FoodLogs
            .FirstOrDefaultAsync(f => f.Id == foodLogId && f.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Блюдо не найдено.");

        if (isFavorite)
        {
            await UpsertFromFoodLogAsync(log, true, cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(log.DishName))
        {
            return;
        }

        var key = log.DishName.Trim().ToLowerInvariant();
        var dish = await _db.SavedDishes
            .FirstOrDefaultAsync(d => d.UserId == userId && d.NameKey == key, cancellationToken);

        if (dish is not null)
        {
            dish.IsFavorite = false;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private static SavedDishDto ToDto(SavedDish d) => new(
        d.Id,
        d.Name,
        d.CaloriesMin,
        d.CaloriesMax,
        d.ProteinMinG,
        d.ProteinMaxG,
        d.FatMinG,
        d.FatMaxG,
        d.CarbsMinG,
        d.CarbsMaxG,
        d.IsFavorite,
        d.UseCount,
        d.LastUsedAt);
}
