namespace FinanceFoodTracker.Application.Common.Options;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public decimal ConfidenceThreshold { get; set; } = 0.6m;
}
