namespace FinanceFoodTracker.Application.Statements;

public interface IStatementProcessor
{
    Task ProcessAsync(Guid statementId, CancellationToken cancellationToken = default);
}
