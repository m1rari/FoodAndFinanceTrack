namespace FinanceFoodTracker.Domain.Entities;

public class Account : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string Name { get; set; } = "Основной";
    public string Currency { get; set; } = "BYN";
}
