using System.Globalization;
using System.Text.Json;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Common.Options;
using FinanceFoodTracker.Application.FoodLogs.Analysis;
using FinanceFoodTracker.Application.SavedDishes;
using FinanceFoodTracker.Domain.Entities;
using FinanceFoodTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceFoodTracker.Application.FoodLogs;

public sealed class FoodLogProcessor : IFoodLogProcessor
{
    private readonly IApplicationDbContext _db;
    private readonly IFoodImageAnalyzer _analyzer;
    private readonly ITelegramBot _bot;
    private readonly ISavedDishService _savedDishes;
    private readonly AiOptions _aiOptions;
    private readonly ILogger<FoodLogProcessor> _logger;

    public FoodLogProcessor(
        IApplicationDbContext db,
        IFoodImageAnalyzer analyzer,
        ITelegramBot bot,
        ISavedDishService savedDishes,
        IOptions<AiOptions> aiOptions,
        ILogger<FoodLogProcessor> logger)
    {
        _db = db;
        _analyzer = analyzer;
        _bot = bot;
        _savedDishes = savedDishes;
        _aiOptions = aiOptions.Value;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid foodLogId, bool single = false, CancellationToken cancellationToken = default)
    {
        var log = await _db.FoodLogs.FirstOrDefaultAsync(f => f.Id == foodLogId, cancellationToken);

        if (log is null || log.Status != ProcessingStatus.Pending)
        {
            return;
        }

        try
        {
            var request = new FoodAnalysisRequest(log.ImagePath, log.Id.ToString(), log.UserContext, log.PortionGrams, single);
            var result = await _analyzer.AnalyzeAsync(request, cancellationToken);

            ApplyResult(log, result, single);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "Не удалось проанализировать блюдо {FoodLogId}", foodLogId);
            log.Status = ProcessingStatus.Failed;
        }

        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _savedDishes.UpsertFromFoodLogAsync(log, null, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Не удалось сохранить блюдо в недавние {FoodLogId}", log.Id);
        }

        if (log.TelegramChatId is long chatId)
        {
            await NotifyTelegramAsync(chatId, log, cancellationToken);
        }
    }

    private void ApplyResult(FoodLog log, FoodAnalysisResult result, bool single)
    {
        var items = result.Items;

        if (items.Count == 0)
        {
            log.Status = ProcessingStatus.NeedsReview;
            return;
        }

        if (single || items.Count == 1)
        {
            ApplyItem(log, items[0], result.RawResponse);
            return;
        }

        var groupId = log.MealGroupId ?? Guid.NewGuid();
        log.MealGroupId = groupId;
        ApplyItem(log, items[0], result.RawResponse, groupId);

        for (var index = 1; index < items.Count; index++)
        {
            var sibling = new FoodLog
            {
                UserId = log.UserId,
                ImagePath = log.ImagePath,
                UserContext = log.UserContext,
                MealGroupId = groupId,
                EatenAt = log.EatenAt
            };

            ApplyItem(sibling, items[index], null, groupId);
            _db.FoodLogs.Add(sibling);
        }
    }

    private void ApplyItem(FoodLog target, FoodAnalysisItemResult item, string? rawResponse, Guid? groupId = null)
    {
        var threshold = _aiOptions.ConfidenceThreshold;

        target.DishName = string.IsNullOrWhiteSpace(item.DishName) ? null : item.DishName.Trim();

        if (target.PortionGrams is not > 0 && item.PortionGrams is > 0)
        {
            target.PortionGrams = item.PortionGrams;
        }

        target.CaloriesMin = item.CaloriesMin;
        target.CaloriesMax = item.CaloriesMax;
        target.ProteinMinG = item.ProteinMinG;
        target.ProteinMaxG = item.ProteinMaxG;
        target.FatMinG = item.FatMinG;
        target.FatMaxG = item.FatMaxG;
        target.CarbsMinG = item.CarbsMinG;
        target.CarbsMaxG = item.CarbsMaxG;
        target.ProteinG = Midpoint(item.ProteinMinG, item.ProteinMaxG);
        target.FatG = Midpoint(item.FatMinG, item.FatMaxG);
        target.CarbsG = Midpoint(item.CarbsMinG, item.CarbsMaxG);
        target.AiRawResponse = EnsureJson(rawResponse);

        if (groupId is not null)
        {
            target.MealGroupId = groupId;
        }

        var hasDish = !string.IsNullOrWhiteSpace(target.DishName)
            && !string.Equals(target.DishName, "Не определено", StringComparison.OrdinalIgnoreCase);
        var hasCalories = target.CaloriesMin is > 0 || target.CaloriesMax is > 0;
        var lowConfidence = item.Confidence is not null && item.Confidence < threshold;

        target.Status = (hasDish || hasCalories) && !lowConfidence
            ? ProcessingStatus.Processed
            : ProcessingStatus.NeedsReview;
    }

    private async Task NotifyTelegramAsync(long chatId, FoodLog log, CancellationToken cancellationToken)
    {
        try
        {
            var text = log.Status switch
            {
                ProcessingStatus.Processed =>
                    $"✅ Блюдо добавлено: {log.DishName ?? "блюдо"} · {FormatRange(log.CaloriesMin, log.CaloriesMax)} ккал (оценка).\nОткройте приложение, чтобы посмотреть детали.",
                ProcessingStatus.NeedsReview =>
                    "⚠️ Не удалось уверенно оценить блюдо. Проверьте в приложении.",
                ProcessingStatus.Failed =>
                    "⚠️ Не удалось оценить блюдо. Добавьте его вручную.",
                _ => null
            };

            if (text is not null)
            {
                await _bot.SendMessageAsync(chatId, text, cancellationToken);
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Не удалось отправить уведомление в Telegram для блюда {FoodLogId}", log.Id);
        }
    }

    private static string FormatRange(decimal? min, decimal? max)
    {
        var low = (min ?? max)?.ToString("0", CultureInfo.InvariantCulture);
        var high = (max ?? min)?.ToString("0", CultureInfo.InvariantCulture);

        if (low is null || high is null)
        {
            return "—";
        }

        return low == high ? low : $"{low}–{high}";
    }

    internal static decimal? Midpoint(decimal? min, decimal? max)
    {
        if (min is null && max is null)
        {
            return null;
        }

        if (min is null)
        {
            return max;
        }

        if (max is null)
        {
            return min;
        }

        return Math.Round((min.Value + max.Value) / 2m, 2, MidpointRounding.AwayFromZero);
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
