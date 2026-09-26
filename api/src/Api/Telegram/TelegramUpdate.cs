using System.Text.Json.Serialization;

namespace FinanceFoodTracker.Api.Telegram;

public sealed class TelegramUpdate
{
    [JsonPropertyName("message")]
    public TelegramMessage? Message { get; set; }
}

public sealed class TelegramMessage
{
    [JsonPropertyName("message_id")]
    public long MessageId { get; set; }

    [JsonPropertyName("chat")]
    public TelegramChat? Chat { get; set; }

    [JsonPropertyName("from")]
    public TelegramUser? From { get; set; }

    [JsonPropertyName("photo")]
    public List<TelegramPhotoSize>? Photo { get; set; }

    [JsonPropertyName("document")]
    public TelegramDocument? Document { get; set; }

    [JsonPropertyName("voice")]
    public TelegramVoice? Voice { get; set; }

    [JsonPropertyName("audio")]
    public TelegramAudio? Audio { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("caption")]
    public string? Caption { get; set; }
}

public sealed class TelegramVoice
{
    [JsonPropertyName("file_id")]
    public string FileId { get; set; } = string.Empty;
}

public sealed class TelegramAudio
{
    [JsonPropertyName("file_id")]
    public string FileId { get; set; } = string.Empty;
}

public sealed class TelegramChat
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
}

public sealed class TelegramUser
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }
}

public sealed class TelegramPhotoSize
{
    [JsonPropertyName("file_id")]
    public string FileId { get; set; } = string.Empty;

    [JsonPropertyName("file_size")]
    public long? FileSize { get; set; }
}

public sealed class TelegramDocument
{
    [JsonPropertyName("file_id")]
    public string FileId { get; set; } = string.Empty;

    [JsonPropertyName("file_name")]
    public string? FileName { get; set; }

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; set; }
}
