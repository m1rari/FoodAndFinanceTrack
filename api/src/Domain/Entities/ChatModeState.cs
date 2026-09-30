namespace FinanceFoodTracker.Domain.Entities;

public class ChatModeState : Entity
{
    public long ChatId { get; set; }
    public int Mode { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
