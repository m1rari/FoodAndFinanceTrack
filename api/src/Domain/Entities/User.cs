namespace FinanceFoodTracker.Domain.Entities;

public class User : Entity
{
    public long TelegramId { get; set; }
    public string? Username { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
