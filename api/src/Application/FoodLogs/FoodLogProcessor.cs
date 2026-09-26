using System.Globalization;
using System.Text.Json;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Common.Options;
using FinanceFoodTracker.Application.FoodLogs.Analysis;
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
    private readonly AiOptions _aiOptions;
    private readonly ILogger<FoodLogProcessor> _logger;

    public FoodLogProcessor(
        IApplicationDbContext db,
        IFoodImageAnalyzer analyzer,
        ITelegramBot bot,
        IOptions<AiOptions> aiOptions,
        ILogger<FoodLogProcessor> logger)
    {
        _db = db;
        _analyzer = analyzer;
        _bot = bot;
        _aiOptions = aiOptions.Value;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid foodLogId, CancellationToken cancellationToken = default)
    {
        var log = await _db.FoodLogs.FirstOrDefaultAsync(f => f.Id == foodLogId, cancellationToken);

        if (log is null || log.Status != ProcessingStatus.Pending)
        {
            return;
        }

        try
        {
            var request = new FoodAnalysisRequest(log.ImagePath, log.Id.ToString(), log.UserContext);
            var result = await _analyzer.AnalyzeAsync(request, cancellationToken);

            ApplyResult(log, result);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "Не удалось проанализировать блюдо {FoodLogId}", foodLogId);
            log.Status = ProcessingStatus.Failed;
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (log.TelegramChatId is long chatId)
        {
            await NotifyTelegramAsync(chatId, log, cancellationToken);
        }
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

    private void ApplyResult(FoodLog log, FoodAnalysisResult result)
    {
        var threshold = _aiOptions.ConfidenceThreshold;

        log.DishName = string.IsNullOrWhiteSpace(result.DishName) ? null : result.DishName.Trim();
        log.CaloriesMin = result.CaloriesMin;
        log.CaloriesMax = result.CaloriesMax;
        log.ProteinMinG = result.ProteinMinG;
        log.ProteinMaxG = result.ProteinMaxG;
        log.FatMinG = result.FatMinG;
        log.FatMaxG = result.FatMaxG;
        log.CarbsMinG = result.CarbsMinG;
        log.CarbsMaxG = result.CarbsMaxG;
        log.ProteinG = Midpoint(result.ProteinMinG, result.ProteinMaxG);
        log.FatG = Midpoint(result.FatMinG, result.FatMaxG);
        log.CarbsG = Midpoint(result.CarbsMinG, result.CarbsMaxG);
        log.AiRawResponse = EnsureJson(result.RawResponse);

        var hasDish = !string.IsNullOrWhiteSpace(log.DishName)
            && !string.Equals(log.DishName, "Не определено", StringComparison.OrdinalIgnoreCase);
        var hasCalories = log.CaloriesMin is > 0 || log.CaloriesMax is > 0;
        var hasData = hasDish || hasCalories;
        var lowConfidence = result.Confidence is not null && result.Confidence < threshold;

        log.Status = hasData && !lowConfidence ? ProcessingStatus.Processed : ProcessingStatus.NeedsReview;
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
