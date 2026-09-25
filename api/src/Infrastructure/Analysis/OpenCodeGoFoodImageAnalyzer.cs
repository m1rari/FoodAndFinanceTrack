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
        Ты — нутрициолог. По фотографии оцени блюдо и верни СТРОГО один валидный JSON-объект
        без markdown, пояснений и текста вокруг.
        Схема:
        {
          "dish_name": string,
          "calories_min": number,
          "calories_max": number,
          "protein_min_g": number,
          "protein_max_g": number,
          "fat_min_g": number,
          "fat_max_g": number,
          "carbs_min_g": number,
          "carbs_max_g": number,
          "confidence": number от 0 до 1,
          "raw_text": string
        }
        Все значения — оценка диапазоном (min/max), калории в ккал, БЖУ в граммах.
        Если блюдо не видно — "dish_name": "Не определено", числовые значения 0.
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

        var userText = string.IsNullOrWhiteSpace(request.Context)
            ? "Оцени блюдо на изображении и верни JSON по заданной схеме."
            : $"Оцени блюдо на изображении и верни JSON по заданной схеме. " +
              $"Контекст от пользователя (учитывай при оценке): {request.Context.Trim()}";

        var content = await _client.CompleteJsonAsync(
            SystemPrompt,
            userText,
            dataUrl,
            request.SessionId,
            cancellationToken);

        var payload = ParsePayload(content);

        if (payload is null)
        {
            return new FoodAnalysisResult(RawResponse: SafeJson(content), RawText: content);
        }

        return new FoodAnalysisResult(
            payload.DishName,
            payload.CaloriesMin,
            payload.CaloriesMax,
            payload.ProteinMinG,
            payload.ProteinMaxG,
            payload.FatMinG,
            payload.FatMaxG,
            payload.CarbsMinG,
            payload.CarbsMaxG,
            payload.Confidence,
            payload.RawText,
            SafeJson(content));
    }

    private async Task<string> BuildDataUrlAsync(string imagePath, CancellationToken cancellationToken)
    {
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
