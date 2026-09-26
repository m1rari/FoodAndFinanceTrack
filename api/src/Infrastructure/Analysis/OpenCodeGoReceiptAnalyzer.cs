using System.Globalization;
using System.Text.Json;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Receipts.Analysis;

namespace FinanceFoodTracker.Infrastructure.Analysis;

public sealed class OpenCodeGoReceiptAnalyzer : IReceiptAnalyzer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly OpenCodeGoVisionClient _client;
    private readonly IFileStorage _fileStorage;

    public OpenCodeGoReceiptAnalyzer(OpenCodeGoVisionClient client, IFileStorage fileStorage)
    {
        _client = client;
        _fileStorage = fileStorage;
    }

    public async Task<ReceiptAnalysisResult> AnalyzeAsync(ReceiptAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        var dataUrl = await BuildDataUrlAsync(request.ImagePath, cancellationToken);

        var baseText = dataUrl is null
            ? "Разбери описание чека и верни JSON по заданной схеме."
            : "Распознай чек на изображении и верни JSON по заданной схеме.";

        var userText = string.IsNullOrWhiteSpace(request.Context)
            ? baseText
            : $"{baseText} Описание от пользователя: {request.Context.Trim()}";

        var content = await _client.CompleteJsonAsync(
            BuildSystemPrompt(request.Categories),
            userText,
            dataUrl,
            request.SessionId,
            cancellationToken);

        var payload = ParsePayload(content);

        if (payload is null)
        {
            return new ReceiptAnalysisResult(RawResponse: SafeJson(content), RawOcrText: content);
        }

        return ToResult(payload, SafeJson(content));
    }

    private async Task<string?> BuildDataUrlAsync(string imagePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return null;
        }

        var imageBytes = await _fileStorage.ReadAsync(imagePath, cancellationToken);
        return $"data:{MediaType(imagePath)};base64,{Convert.ToBase64String(imageBytes)}";
    }

    private static string BuildSystemPrompt(IReadOnlyList<string> categories)
    {
        var categoryList = categories.Count > 0 ? string.Join(", ", categories) : "нет";

        return $$"""
            Ты — ассистент для разбора фотографий кассовых чеков.
            Извлеки данные чека и верни СТРОГО один валидный JSON-объект без markdown, пояснений и текста вокруг.
            Схема:
            {
              "merchant_name": string | null,
              "purchase_date": "YYYY-MM-DD" | null,
              "total_amount": number | null,
              "confidence": number от 0 до 1,
              "raw_text": string,
              "items": [
                {
                  "name": string,
                  "quantity": number,
                  "unit_price": number,
                  "total_price": number,
                  "category": string | null,
                  "confidence": number от 0 до 1
                }
              ]
            }
            Категорию каждой позиции выбери СТРОГО из списка: {{categoryList}}.
            Если ничего не подходит — верни null. Суммы — в валюте чека без символов.
            """;
    }

    private static ReceiptPayload? ParsePayload(string content)
    {
        var json = ExtractJson(content);

        if (json is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ReceiptPayload>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ReceiptAnalysisResult ToResult(ReceiptPayload payload, string rawJson)
    {
        var items = new List<ReceiptAnalysisItem>();

        foreach (var item in payload.Items ?? new List<ReceiptPayloadItem>())
        {
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                continue;
            }

            var quantity = item.Quantity is > 0 ? item.Quantity.Value : 1m;
            var total = item.TotalPrice ?? (item.UnitPrice is not null ? item.UnitPrice.Value * quantity : 0m);
            var unit = item.UnitPrice ?? (quantity > 0 ? total / quantity : 0m);

            items.Add(new ReceiptAnalysisItem(
                item.Name.Trim(),
                quantity,
                Round(unit),
                Round(total),
                item.Category,
                item.Confidence));
        }

        return new ReceiptAnalysisResult(
            payload.MerchantName,
            ParseDate(payload.PurchaseDate),
            payload.TotalAmount,
            payload.RawText,
            rawJson,
            payload.Confidence,
            items);
    }

    private static DateTimeOffset? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string? ExtractJson(string content)
    {
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');

        return start >= 0 && end > start ? content[start..(end + 1)] : null;
    }

    private static string SafeJson(string content)
    {
        try
        {
            using var _ = JsonDocument.Parse(content);
            return content;
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(new { raw = content });
        }
    }

    private static string MediaType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg"
    };
}
