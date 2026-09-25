namespace FinanceFoodTracker.Application.Common.Options;

public sealed class OpenCodeGoOptions
{
    public const string SectionName = "OpenCodeGo";

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://opencode.ai/zen/go/v1";

    public string Model { get; set; } = "deepseek-v4-flash-vision-exp";

    public int TimeoutSeconds { get; set; } = 120;
}
