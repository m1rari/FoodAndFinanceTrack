namespace FinanceFoodTracker.Application.Common.Options;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = string.Empty;

    public TimeSpan InitDataTtl { get; set; } = TimeSpan.FromHours(24);

    public string WebhookSecret { get; set; } = string.Empty;

    public string PublicBaseUrl { get; set; } = string.Empty;
}
