namespace FinanceFoodTracker.Application.Transactions;

public interface ITransactionService
{
    Task<IReadOnlyList<TransactionDto>> GetAsync(Guid userId, TransactionFilter filter, CancellationToken cancellationToken = default);
    Task<TransactionDto> CreateAsync(Guid userId, CreateTransactionRequest request, CancellationToken cancellationToken = default);
    Task<TransactionDto> UpdateAsync(Guid userId, Guid id, UpdateTransactionRequest request, CancellationToken cancellationToken = default);
}
