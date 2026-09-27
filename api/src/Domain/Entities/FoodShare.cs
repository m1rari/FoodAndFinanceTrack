namespace FinanceFoodTracker.Domain.Entities;

public class FoodShare : Entity
{
    public string Token { get; set; } = string.Empty;
    public Guid OwnerUserId { get; set; }
    public User? Owner { get; set; }
    public string DishName { get; set; } = string.Empty;
    public string? UserContext { get; set; }
    public decimal? CaloriesMin { get; set; }
    public decimal? CaloriesMax { get; set; }
    public decimal? ProteinMinG { get; set; }
    public decimal? ProteinMaxG { get; set; }
    public decimal? FatMinG { get; set; }
    public decimal? FatMaxG { get; set; }
    public decimal? CarbsMinG { get; set; }
    public decimal? CarbsMaxG { get; set; }
    public int ClaimCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
}
