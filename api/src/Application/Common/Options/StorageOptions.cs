namespace FinanceFoodTracker.Application.Common.Options;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string RootPath { get; set; } = "uploads";
}
