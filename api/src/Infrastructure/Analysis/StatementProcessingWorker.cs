using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Statements;
using FinanceFoodTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinanceFoodTracker.Infrastructure.Analysis;

public sealed class StatementProcessingWorker : BackgroundService
{
    private readonly IStatementProcessingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StatementProcessingWorker> _logger;

    public StatementProcessingWorker(
        IStatementProcessingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<StatementProcessingWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverPendingAsync(stoppingToken);

        await foreach (var statementId in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IStatementProcessor>();
                await processor.ProcessAsync(statementId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Ошибка фонового разбора выписки {StatementId}", statementId);
            }
        }
    }

    private async Task RecoverPendingAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

            var pending = await db.Statements
                .Where(s => s.Status == ProcessingStatus.Pending)
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);

            foreach (var statementId in pending)
            {
                await _queue.EnqueueAsync(statementId, cancellationToken);
            }

            if (pending.Count > 0)
            {
                _logger.LogInformation("Восстановлено задач разбора выписок: {Count}", pending.Count);
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Не удалось восстановить очередь разбора выписок");
        }
    }
}
