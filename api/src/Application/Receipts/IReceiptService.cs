namespace FinanceFoodTracker.Application.Receipts;

public interface IReceiptService
{
    Task<ReceiptDto> CreateAsync(Guid userId, byte[] content, string fileName, CancellationToken cancellationToken = default);

    Task<ReceiptDto> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<ReceiptImageDto> GetImageAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<ReceiptDto> AddItemAsync(Guid userId, Guid receiptId, CreateReceiptItemRequest request, CancellationToken cancellationToken = default);

    Task<ReceiptDto> UpdateItemAsync(Guid userId, Guid receiptId, Guid itemId, UpdateReceiptItemRequest request, CancellationToken cancellationToken = default);
}
