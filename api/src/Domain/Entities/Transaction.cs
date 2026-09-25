using FinanceFoodTracker.Domain.Enums;

namespace FinanceFoodTracker.Domain.Entities;

public class Transaction : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "BYN";
    public DateTimeOffset OccurredAt { get; set; }
    public TransactionSource Source { get; set; } = TransactionSource.Manual;
    public string? Comment { get; set; }
    public Guid? ReceiptId { get; set; }
    public Receipt? Receipt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
