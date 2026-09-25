namespace FinanceFoodTracker.Domain.Entities;

public class ReceiptItem : Entity
{
    public Guid ReceiptId { get; set; }
    public Receipt? Receipt { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }
    public decimal? Confidence { get; set; }
}
