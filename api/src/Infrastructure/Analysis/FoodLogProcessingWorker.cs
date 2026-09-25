using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.FoodLogs;
using FinanceFoodTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinanceFoodTracker.Infrastructure.Analysis;

public sealed class FoodLogProcessingWorker : BackgroundService
{
    private readonly IFoodLogProcessingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FoodLogProcessingWorker> _logger;

    public FoodLogProcessingWorker(
        IFoodLogProcessingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<FoodLogProcessingWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverPendingAsync(stoppingToken);

        await foreach (var foodLogId in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IFoodLogProcessor>();
                await processor.ProcessAsync(foodLogId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Ошибка фоновой обработки блюда {FoodLogId}", foodLogId);
            }
        }
    }

    private async Task RecoverPendingAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

            var pending = await db.FoodLogs
                .Where(f => f.Status == ProcessingStatus.Pending)
                .Select(f => f.Id)
                .ToListAsync(cancellationToken);

            foreach (var foodLogId in pending)
            {
                await _queue.EnqueueAsync(foodLogId, cancellationToken);
            }

            if (pending.Count > 0)
            {
                _logger.LogInformation("Восстановлено задач анализа блюд: {Count}", pending.Count);
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Не удалось восстановить очередь анализа блюд");
        }
    }
}
