using FinanceFoodTracker.Domain.Entities;

namespace FinanceFoodTracker.Application.FoodLogs;

public static class FoodLogMapper
{
    public static FoodLogDto ToDto(FoodLog f) => new(
        f.Id,
        f.DishName,
        f.UserContext,
        f.CaloriesMin,
        f.CaloriesMax,
        f.ProteinMinG,
        f.ProteinMaxG,
        f.FatMinG,
        f.FatMaxG,
        f.CarbsMinG,
        f.CarbsMaxG,
        f.ProteinG,
        f.FatG,
        f.CarbsG,
        f.Status.ToString(),
        f.EatenAt,
        $"/api/food-logs/{f.Id}/image",
        f.CreatedAt);
}
