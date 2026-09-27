using System.Text.Json.Serialization;

namespace FinanceFoodTracker.Infrastructure.Analysis;

internal sealed class FoodPayload
{
    [JsonPropertyName("items")]
    public List<FoodPayloadItem>? Items { get; set; }
}

internal sealed class FoodPayloadItem
{
    [JsonPropertyName("dish_name")]
    public string? DishName { get; set; }

    [JsonPropertyName("portion_grams")]
    public decimal? PortionGrams { get; set; }

    [JsonPropertyName("calories_min")]
    public decimal? CaloriesMin { get; set; }

    [JsonPropertyName("calories_max")]
    public decimal? CaloriesMax { get; set; }

    [JsonPropertyName("protein_min_g")]
    public decimal? ProteinMinG { get; set; }

    [JsonPropertyName("protein_max_g")]
    public decimal? ProteinMaxG { get; set; }

    [JsonPropertyName("fat_min_g")]
    public decimal? FatMinG { get; set; }

    [JsonPropertyName("fat_max_g")]
    public decimal? FatMaxG { get; set; }

    [JsonPropertyName("carbs_min_g")]
    public decimal? CarbsMinG { get; set; }

    [JsonPropertyName("carbs_max_g")]
    public decimal? CarbsMaxG { get; set; }

    [JsonPropertyName("confidence")]
    public decimal? Confidence { get; set; }
}
