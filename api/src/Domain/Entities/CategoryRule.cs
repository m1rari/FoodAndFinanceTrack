namespace FinanceFoodTracker.Domain.Entities;

public class CategoryRule : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string MatchPattern { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
