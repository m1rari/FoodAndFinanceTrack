using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Common.Options;
using FinanceFoodTracker.Application.Receipts.Analysis;
using Microsoft.Extensions.Options;

namespace FinanceFoodTracker.Infrastructure.Analysis;

public sealed class OpenCodeGoReceiptAnalyzer : IReceiptAnalyzer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly IFileStorage _fileStorage;
    private readonly OpenCodeGoOptions _options;

    public OpenCodeGoReceiptAnalyzer(HttpClient http, IFileStorage fileStorage, IOptions<OpenCodeGoOptions> options)
    {
        _http = http;
        _fileStorage = fileStorage;
        _options = options.Value;
    }

    public async Task<ReceiptAnalysisResult> AnalyzeAsync(ReceiptAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Ключ OpenCode Go API не настроен.");
        }

        var imageBytes = await _fileStorage.ReadAsync(request.ImagePath, cancellationToken);
        var dataUrl = $"data:{ContentTypeFor(request.ImagePath)};base64,{Convert.ToBase64String(imageBytes)}";

        var completion = await CompleteAsync(request, dataUrl, useResponseFormat: true, cancellationToken);
        var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("AI-провайдер вернул пустой ответ.");
        }

        var payload = ParsePayload(content);

        if (payload is null)
        {
            return new ReceiptAnalysisResult(RawResponse: SafeJson(content), RawOcrText: content);
        }

        return ToResult(payload, SafeJson(content));
    }

    private async Task<ChatCompletionResponse?> CompleteAsync(
        ReceiptAnalysisRequest request,
        string dataUrl,
        bool useResponseFormat,
        CancellationToken cancellationToken)
    {
        var body = BuildRequest(request, dataUrl, useResponseFormat);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(body, options: SerializerOptions)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        using var response = await _http.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            if (useResponseFormat && response.StatusCode == HttpStatusCode.BadRequest)
            {
                return await CompleteAsync(request, dataUrl, useResponseFormat: false, cancellationToken);
            }

            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"AI-провайдер вернул {(int)response.StatusCode}: {Truncate(error, 300)}");
        }

        return await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(SerializerOptions, cancellationToken);
    }

    private ChatCompletionRequest BuildRequest(ReceiptAnalysisRequest request, string dataUrl, bool useResponseFormat)
    {
        var categories = request.Categories.Count > 0 ? string.Join(", ", request.Categories) : "нет";

        var system = $$"""
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
            Категорию каждой позиции выбери СТРОГО из списка: {{categories}}.
            Если ничего не подходит — верни null. Суммы — в валюте чека без символов.
            """;

        var userText = "Распознай чек на изображении и верни JSON по заданной схеме.";

        return new ChatCompletionRequest
        {
            Model = _options.Model,
            RequestFormat = useResponseFormat ? new ResponseFormat() : null,
            Messages = new List<ChatMessage>
            {
                new()
                {
                    Role = "system",
                    Content = new List<ContentPart>
                    {
                        new() { Type = "text", Text = system }
                    }
                },
                new()
                {
                    Role = "user",
                    Content = new List<ContentPart>
                    {
                        new() { Type = "text", Text = userText },
                        new() { Type = "image_url", ImageUrl = new ImageUrl { Url = dataUrl } }
                    }
                }
            }
        };
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

    private static string ContentTypeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg"
    };

    private static string Truncate(string value, int length)
        => value.Length <= length ? value : value[..length];
}
