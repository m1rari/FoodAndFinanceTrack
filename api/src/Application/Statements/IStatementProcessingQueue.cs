namespace FinanceFoodTracker.Application.Statements;

public interface IStatementProcessingQueue
{
    ValueTask EnqueueAsync(Guid statementId, CancellationToken cancellationToken = default);

    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken = default);
}
