namespace FinanceFoodTracker.Domain.Entities;

public class SavedDish : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameKey { get; set; } = string.Empty;
    public decimal? CaloriesMin { get; set; }
    public decimal? CaloriesMax { get; set; }
    public decimal? ProteinMinG { get; set; }
    public decimal? ProteinMaxG { get; set; }
    public decimal? FatMinG { get; set; }
    public decimal? FatMaxG { get; set; }
    public decimal? CarbsMinG { get; set; }
    public decimal? CarbsMaxG { get; set; }
    public bool IsFavorite { get; set; }
    public int UseCount { get; set; }
    public DateTimeOffset LastUsedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
