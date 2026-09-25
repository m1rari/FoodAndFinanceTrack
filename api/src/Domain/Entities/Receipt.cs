using FinanceFoodTracker.Domain.Enums;

namespace FinanceFoodTracker.Domain.Entities;

public class Receipt : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string ImagePath { get; set; } = string.Empty;
    public string? MerchantName { get; set; }
    public DateTimeOffset? PurchaseDate { get; set; }
    public decimal? TotalAmount { get; set; }
    public string? RawOcrText { get; set; }
    public string? AiRawResponse { get; set; }
    public ProcessingStatus Status { get; set; } = ProcessingStatus.Pending;
    public decimal? Confidence { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ReceiptItem> Items { get; set; } = new();
}
