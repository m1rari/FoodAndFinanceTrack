namespace FinanceFoodTracker.Application.Receipts;

public interface IReceiptService
{
    Task<ReceiptDto> CreateAsync(Guid userId, byte[] content, string fileName, CancellationToken cancellationToken = default, long? telegramChatId = null);

    Task<ReceiptDto> CreateFromTextAsync(Guid userId, string text, CancellationToken cancellationToken = default, long? telegramChatId = null);

    Task<IReadOnlyList<ReceiptSummaryDto>> GetListAsync(Guid userId, bool onlyUnconfirmed = false, CancellationToken cancellationToken = default);

    Task<ReceiptDto> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<ReceiptImageDto> GetImageAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<ReceiptDto> AddItemAsync(Guid userId, Guid receiptId, CreateReceiptItemRequest request, CancellationToken cancellationToken = default);

    Task<ReceiptDto> UpdateItemAsync(Guid userId, Guid receiptId, Guid itemId, UpdateReceiptItemRequest request, CancellationToken cancellationToken = default);

    Task<ReceiptDto> ConfirmAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Transactions.TransactionDto>> GetMatchesAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<ReceiptDto> LinkAsync(Guid userId, Guid id, Guid transactionId, CancellationToken cancellationToken = default);
}
