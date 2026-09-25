using FinanceFoodTracker.Domain.Enums;

namespace FinanceFoodTracker.Domain.Entities;

public class Category : Entity
{
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    public string Name { get; set; } = string.Empty;
    public TransactionType Type { get; set; }
    public Guid? ParentId { get; set; }
    public Category? Parent { get; set; }
    public bool IsSystem { get; set; }
}
