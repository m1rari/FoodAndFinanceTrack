namespace FinanceFoodTracker.Application.Receipts;

public interface IReceiptProcessor
{
    Task ProcessAsync(Guid receiptId, CancellationToken cancellationToken = default);
}
