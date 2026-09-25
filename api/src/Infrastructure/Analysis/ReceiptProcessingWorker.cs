using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Receipts;
using FinanceFoodTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinanceFoodTracker.Infrastructure.Analysis;

public sealed class ReceiptProcessingWorker : BackgroundService
{
    private readonly IReceiptProcessingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReceiptProcessingWorker> _logger;

    public ReceiptProcessingWorker(
        IReceiptProcessingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ReceiptProcessingWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverPendingAsync(stoppingToken);

        await foreach (var receiptId in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IReceiptProcessor>();
                await processor.ProcessAsync(receiptId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Ошибка фоновой обработки чека {ReceiptId}", receiptId);
            }
        }
    }

    private async Task RecoverPendingAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

            var pending = await db.Receipts
                .Where(r => r.Status == ProcessingStatus.Pending)
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);

            foreach (var receiptId in pending)
            {
                await _queue.EnqueueAsync(receiptId, cancellationToken);
            }

            if (pending.Count > 0)
            {
                _logger.LogInformation("Восстановлено задач анализа чеков: {Count}", pending.Count);
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Не удалось восстановить очередь анализа чеков");
        }
    }
}
