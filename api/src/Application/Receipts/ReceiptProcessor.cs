using System.Globalization;
using System.Text.Json;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Common.Options;
using FinanceFoodTracker.Application.Receipts.Analysis;
using FinanceFoodTracker.Domain.Entities;
using FinanceFoodTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceFoodTracker.Application.Receipts;

public sealed class ReceiptProcessor : IReceiptProcessor
{
    private readonly IApplicationDbContext _db;
    private readonly IReceiptAnalyzer _analyzer;
    private readonly ITelegramBot _bot;
    private readonly AiOptions _aiOptions;
    private readonly ILogger<ReceiptProcessor> _logger;

    public ReceiptProcessor(
        IApplicationDbContext db,
        IReceiptAnalyzer analyzer,
        ITelegramBot bot,
        IOptions<AiOptions> aiOptions,
        ILogger<ReceiptProcessor> logger)
    {
        _db = db;
        _analyzer = analyzer;
        _bot = bot;
        _aiOptions = aiOptions.Value;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid receiptId, CancellationToken cancellationToken = default)
    {
        var receipt = await _db.Receipts.FirstOrDefaultAsync(r => r.Id == receiptId, cancellationToken);

        if (receipt is null || receipt.Status != ProcessingStatus.Pending)
        {
            return;
        }

        try
        {
            var categories = await _db.Categories
                .Where(c => c.Type == TransactionType.Expense && (c.UserId == null || c.UserId == receipt.UserId))
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);

            var request = new ReceiptAnalysisRequest(
                receipt.ImagePath,
                categories.Select(c => c.Name).ToList(),
                receipt.Id.ToString(),
                receipt.UserContext);
            var result = await _analyzer.AnalyzeAsync(request, cancellationToken);

            ApplyResult(receipt, result, categories);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "Не удалось проанализировать чек {ReceiptId}", receiptId);
            receipt.Status = ProcessingStatus.Failed;
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (receipt.TelegramChatId is long chatId)
        {
            await NotifyTelegramAsync(chatId, receipt, cancellationToken);
        }
    }

    private async Task NotifyTelegramAsync(long chatId, Receipt receipt, CancellationToken cancellationToken)
    {
        try
        {
            var count = await _db.ReceiptItems.CountAsync(i => i.ReceiptId == receipt.Id, cancellationToken);
            var total = receipt.TotalAmount?.ToString("0.00", CultureInfo.InvariantCulture);

            var text = receipt.Status switch
            {
                ProcessingStatus.Processed =>
                    $"✅ Чек добавлен: {receipt.MerchantName ?? "покупка"} · {total} ({count} {PluralItems(count)}).\nОткройте приложение, чтобы проверить и провести покупку.",
                ProcessingStatus.NeedsReview =>
                    "⚠️ Чек распознан неуверенно. Проверьте позиции в приложении.",
                ProcessingStatus.Failed =>
                    "⚠️ Не удалось распознать чек. Добавьте операцию вручную.",
                _ => null
            };

            if (text is not null)
            {
                await _bot.SendMessageAsync(chatId, text, cancellationToken);
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Не удалось отправить уведомление в Telegram для чека {ReceiptId}", receipt.Id);
        }
    }

    private static string PluralItems(int count)
    {
        var mod10 = count % 10;
        var mod100 = count % 100;

        if (mod10 == 1 && mod100 != 11)
        {
            return "товар";
        }

        if (mod10 >= 2 && mod10 <= 4 && (mod100 < 10 || mod100 >= 20))
        {
            return "товара";
        }

        return "товаров";
    }

    private void ApplyResult(Receipt receipt, ReceiptAnalysisResult result, IReadOnlyList<Category> categories)
    {
        var threshold = _aiOptions.ConfidenceThreshold;
        var needsReview = false;

        receipt.MerchantName = string.IsNullOrWhiteSpace(result.MerchantName) ? null : result.MerchantName.Trim();
        receipt.PurchaseDate = result.PurchaseDate;
        receipt.RawOcrText = result.RawOcrText;
        receipt.AiRawResponse = EnsureJson(result.RawResponse);
        receipt.Confidence = result.Confidence;

        if (result.Confidence is not null && result.Confidence < threshold)
        {
            needsReview = true;
        }

        var validItems = new List<ReceiptItem>();

        foreach (var item in result.SafeItems)
        {
            if (string.IsNullOrWhiteSpace(item.Name) || item.Quantity <= 0 || item.UnitPrice < 0 || item.TotalPrice < 0)
            {
                needsReview = true;
                continue;
            }

            if (item.Confidence is not null && item.Confidence < threshold)
            {
                needsReview = true;
            }

            validItems.Add(new ReceiptItem
            {
                ReceiptId = receipt.Id,
                Name = item.Name.Trim(),
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.TotalPrice,
                CategoryId = MatchCategory(item.CategoryHint, categories),
                Confidence = item.Confidence
            });
        }

        var itemsSum = validItems.Sum(i => i.TotalPrice);

        if (result.TotalAmount is not null && !WithinTolerance(result.TotalAmount.Value, itemsSum))
        {
            needsReview = true;
        }

        foreach (var item in validItems)
        {
            _db.ReceiptItems.Add(item);
        }

        receipt.TotalAmount = result.TotalAmount ?? itemsSum;
        receipt.Status = validItems.Count > 0 && !needsReview
            ? ProcessingStatus.Processed
            : ProcessingStatus.NeedsReview;
    }

    private static bool WithinTolerance(decimal total, decimal itemsSum)
    {
        var tolerance = Math.Max(0.05m, Math.Abs(total) * 0.01m);
        return Math.Abs(total - itemsSum) <= tolerance;
    }

    private static Guid? MatchCategory(string? hint, IReadOnlyList<Category> categories)
    {
        if (string.IsNullOrWhiteSpace(hint))
        {
            return null;
        }

        var normalized = hint.Trim();

        var exact = categories.FirstOrDefault(c => string.Equals(c.Name, normalized, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact.Id;
        }

        var partial = categories.FirstOrDefault(c =>
            c.Name.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(c.Name, StringComparison.OrdinalIgnoreCase));

        return partial?.Id;
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
