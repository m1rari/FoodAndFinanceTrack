using System.Text.Json.Serialization;

namespace FinanceFoodTracker.Infrastructure.Analysis;

internal sealed class StatementPayload
{
    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("opening_balance")]
    public decimal? OpeningBalance { get; set; }

    [JsonPropertyName("closing_balance")]
    public decimal? ClosingBalance { get; set; }

    [JsonPropertyName("operations")]
    public List<StatementPayloadOperation>? Operations { get; set; }
}

internal sealed class StatementPayloadOperation
{
    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("time")]
    public string? Time { get; set; }

    [JsonPropertyName("amount")]
    public decimal? Amount { get; set; }

    [JsonPropertyName("direction")]
    public string? Direction { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("place")]
    public string? Place { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("mcc")]
    public string? Mcc { get; set; }

    [JsonPropertyName("is_transfer")]
    public bool? IsTransfer { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("confidence")]
    public decimal? Confidence { get; set; }
}
