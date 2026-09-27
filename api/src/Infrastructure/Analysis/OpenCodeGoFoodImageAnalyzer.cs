using System.Globalization;
using System.Text.Json;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.FoodLogs.Analysis;

namespace FinanceFoodTracker.Infrastructure.Analysis;

public sealed class OpenCodeGoFoodImageAnalyzer : IFoodImageAnalyzer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string SystemPrompt = """
        Ты — нутрициолог. По фотографии приёма пищи выдели КАЖДОЕ отдельное блюдо/компонент
        (например "борщ", "пюре", "курица") и верни СТРОГО один валидный JSON без markdown:
        {
          "items": [
            {
              "dish_name": string,
              "portion_grams": number,
              "calories_min": number, "calories_max": number,
              "protein_min_g": number, "protein_max_g": number,
              "fat_min_g": number, "fat_max_g": number,
              "carbs_min_g": number, "carbs_max_g": number,
              "confidence": number от 0 до 1
            }
          ]
        }
        Правила:
        - Если на фото несколько блюд — верни их ОТДЕЛЬНЫМИ элементами. Не объединяй "борщ и пюре с курицей" в один.
        - portion_grams — примерная масса порции в граммах (оценка).
        - Все значения — оценка диапазоном (min/max): калории в ккал, БЖУ в граммах.
        - Если пользователь указал массу порции — используй её.
        - Если блюдо не видно — один элемент: "dish_name": "Не определено", числовые значения 0.
        """;

    private readonly OpenCodeGoVisionClient _client;
    private readonly IFileStorage _fileStorage;

    public OpenCodeGoFoodImageAnalyzer(OpenCodeGoVisionClient client, IFileStorage fileStorage)
    {
        _client = client;
        _fileStorage = fileStorage;
    }

    public async Task<FoodAnalysisResult> AnalyzeAsync(FoodAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        var dataUrl = await BuildDataUrlAsync(request.ImagePath, cancellationToken);
        var userText = BuildUserText(request, dataUrl is null);

        var content = await _client.CompleteJsonAsync(SystemPrompt, userText, dataUrl, request.SessionId, cancellationToken);
        var payload = ParsePayload(content);

        if (payload?.Items is null)
        {
            return new FoodAnalysisResult(Array.Empty<FoodAnalysisItemResult>(), content, SafeJson(content));
        }

        var items = payload.Items.Select(Map).ToList();
        return new FoodAnalysisResult(items, null, SafeJson(content));
    }

    private static string BuildUserText(FoodAnalysisRequest request, bool textOnly)
    {
        var parts = new List<string>();

        parts.Add(textOnly
            ? "Оцени блюдо по описанию пользователя и верни JSON по схеме."
            : "Оцени блюда на изображении и верни JSON по схеме.");

        if (request.SingleItem)
        {
            parts.Add("Верни ровно ОДИН элемент для описанного блюда.");
        }

        if (!string.IsNullOrWhiteSpace(request.Context))
        {
            parts.Add($"Контекст: {request.Context.Trim()}.");
        }

        if (request.PortionGrams is > 0)
        {
            parts.Add($"Масса порции: {request.PortionGrams.Value.ToString("0.#", CultureInfo.InvariantCulture)} г.");
        }

        return string.Join(" ", parts);
    }

    private static FoodAnalysisItemResult Map(FoodPayloadItem item) => new(
        item.DishName,
        item.PortionGrams,
        item.CaloriesMin,
        item.CaloriesMax,
        item.ProteinMinG,
        item.ProteinMaxG,
        item.FatMinG,
        item.FatMaxG,
        item.CarbsMinG,
        item.CarbsMaxG,
        item.Confidence);

    private async Task<string?> BuildDataUrlAsync(string imagePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return null;
        }

        var imageBytes = await _fileStorage.ReadAsync(imagePath, cancellationToken);
        return $"data:{MediaType(imagePath)};base64,{Convert.ToBase64String(imageBytes)}";
    }

    private static FoodPayload? ParsePayload(string content)
    {
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<FoodPayload>(content[start..(end + 1)], SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
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
