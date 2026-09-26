namespace FinanceFoodTracker.Application.Statements;

public interface IStatementService
{
    Task<StatementDto> CreateAsync(Guid userId, byte[] pdf, string fileName, CancellationToken cancellationToken = default);

    Task<StatementDto> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<StatementDto> ConfirmAsync(Guid userId, Guid id, ConfirmStatementRequest request, CancellationToken cancellationToken = default);
}
