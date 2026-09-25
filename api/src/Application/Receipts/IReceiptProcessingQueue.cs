namespace FinanceFoodTracker.Application.Receipts;

public interface IReceiptProcessingQueue
{
    ValueTask EnqueueAsync(Guid receiptId, CancellationToken cancellationToken = default);

    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken = default);
}
