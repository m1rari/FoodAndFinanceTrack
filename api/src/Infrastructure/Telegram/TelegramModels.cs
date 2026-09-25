using System.Text.Json.Serialization;

namespace FinanceFoodTracker.Infrastructure.Telegram;

internal sealed class TelegramResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

internal sealed class TelegramFileResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("result")]
    public TelegramFile? Result { get; set; }
}

internal sealed class TelegramFile
{
    [JsonPropertyName("file_path")]
    public string? FilePath { get; set; }
}
