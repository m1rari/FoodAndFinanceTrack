using System.Security.Cryptography;
using FinanceFoodTracker.Application.Common.Exceptions;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Common.Options;
using FinanceFoodTracker.Application.FoodLogs;
using FinanceFoodTracker.Application.SavedDishes;
using FinanceFoodTracker.Domain.Entities;
using FinanceFoodTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FinanceFoodTracker.Application.FoodShares;

public sealed class FoodShareService : IFoodShareService
{
    private readonly IApplicationDbContext _db;
    private readonly ISavedDishService _savedDishes;
    private readonly TelegramOptions _options;

    public FoodShareService(IApplicationDbContext db, ISavedDishService savedDishes, IOptions<TelegramOptions> options)
    {
        _db = db;
        _savedDishes = savedDishes;
        _options = options.Value;
    }

    public async Task<FoodShareDto> CreateAsync(Guid userId, Guid foodLogId, CancellationToken cancellationToken = default)
    {
        var log = await _db.FoodLogs
            .FirstOrDefaultAsync(f => f.Id == foodLogId && f.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Блюдо не найдено.");

        if (string.IsNullOrWhiteSpace(log.DishName))
        {
            throw new ValidationException("Нечего делиться — у блюда нет названия.");
        }

        var share = new FoodShare
        {
            Token = GenerateToken(),
            OwnerUserId = userId,
            DishName = log.DishName.Trim(),
            UserContext = log.UserContext,
            CaloriesMin = log.CaloriesMin,
            CaloriesMax = log.CaloriesMax,
            ProteinMinG = log.ProteinMinG,
            ProteinMaxG = log.ProteinMaxG,
            FatMinG = log.FatMinG,
            FatMaxG = log.FatMaxG,
            CarbsMinG = log.CarbsMinG,
            CarbsMaxG = log.CarbsMaxG,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
        };

        _db.FoodShares.Add(share);
        await _db.SaveChangesAsync(cancellationToken);

        return new FoodShareDto(share.Token, BuildUrl(share.Token));
    }

    public async Task<FoodSharePreviewDto> GetAsync(string token, CancellationToken cancellationToken = default)
    {
        var share = await GetShareAsync(token, cancellationToken);

        return new FoodSharePreviewDto(
            share.DishName,
            share.UserContext,
            share.CaloriesMin,
            share.CaloriesMax,
            FoodLogProcessor.Midpoint(share.ProteinMinG, share.ProteinMaxG),
            FoodLogProcessor.Midpoint(share.FatMinG, share.FatMaxG),
            FoodLogProcessor.Midpoint(share.CarbsMinG, share.CarbsMaxG));
    }

    public async Task<FoodLogDto> ClaimAsync(Guid userId, string token, CancellationToken cancellationToken = default)
    {
        var share = await GetShareAsync(token, cancellationToken);

        var log = new FoodLog
        {
            UserId = userId,
            ImagePath = string.Empty,
            DishName = share.DishName,
            UserContext = share.UserContext,
            CaloriesMin = share.CaloriesMin,
            CaloriesMax = share.CaloriesMax,
            ProteinMinG = share.ProteinMinG,
            ProteinMaxG = share.ProteinMaxG,
            FatMinG = share.FatMinG,
            FatMaxG = share.FatMaxG,
            CarbsMinG = share.CarbsMinG,
            CarbsMaxG = share.CarbsMaxG,
            ProteinG = FoodLogProcessor.Midpoint(share.ProteinMinG, share.ProteinMaxG),
            FatG = FoodLogProcessor.Midpoint(share.FatMinG, share.FatMaxG),
            CarbsG = FoodLogProcessor.Midpoint(share.CarbsMinG, share.CarbsMaxG),
            EatenAt = DateTimeOffset.UtcNow,
            Status = ProcessingStatus.Processed
        };

        share.ClaimCount += 1;

        _db.FoodLogs.Add(log);
        await _db.SaveChangesAsync(cancellationToken);

        await _savedDishes.UpsertFromFoodLogAsync(log, null, cancellationToken);

        return FoodLogMapper.ToDto(log);
    }

    private async Task<FoodShare> GetShareAsync(string token, CancellationToken cancellationToken)
    {
        var share = await _db.FoodShares
            .FirstOrDefaultAsync(s => s.Token == token, cancellationToken)
            ?? throw new NotFoundException("Блюдо не найдено или ссылка недействительна.");

        if (share.ExpiresAt < DateTimeOffset.UtcNow)
        {
            throw new ValidationException("Срок действия ссылки истёк.");
        }

        return share;
    }

    private string? BuildUrl(string token)
        => string.IsNullOrWhiteSpace(_options.BotUsername)
            ? null
            : $"https://t.me/{_options.BotUsername}?startapp=fd-{token}";

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[9];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
