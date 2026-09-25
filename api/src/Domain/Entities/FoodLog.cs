using FinanceFoodTracker.Domain.Enums;

namespace FinanceFoodTracker.Domain.Entities;

public class FoodLog : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string ImagePath { get; set; } = string.Empty;
    public string? DishName { get; set; }
    public decimal? CaloriesMin { get; set; }
    public decimal? CaloriesMax { get; set; }
    public decimal? ProteinG { get; set; }
    public decimal? FatG { get; set; }
    public decimal? CarbsG { get; set; }
    public DateTimeOffset EatenAt { get; set; } = DateTimeOffset.UtcNow;
    public string? AiRawResponse { get; set; }
    public ProcessingStatus Status { get; set; } = ProcessingStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
