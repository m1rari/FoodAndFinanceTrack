using System.Threading.Channels;
using FinanceFoodTracker.Application.Statements;

namespace FinanceFoodTracker.Infrastructure.Analysis;

public sealed class StatementProcessingQueue : IStatementProcessingQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public ValueTask EnqueueAsync(Guid statementId, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(statementId, cancellationToken);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken = default)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}
