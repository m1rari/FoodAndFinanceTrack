using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Common.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceFoodTracker.Infrastructure.Telegram;

public sealed class TelegramWebhookSetup : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramWebhookSetup> _logger;

    public TelegramWebhookSetup(
        IServiceScopeFactory scopeFactory,
        IOptions<TelegramOptions> options,
        ILogger<TelegramWebhookSetup> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BotToken) || string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
        {
            _logger.LogInformation("Регистрация webhook бота пропущена: не заданы BotToken или PublicBaseUrl.");
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var bot = scope.ServiceProvider.GetRequiredService<ITelegramBot>();
            var url = _options.PublicBaseUrl.TrimEnd('/') + "/api/telegram/webhook";
            var secret = string.IsNullOrWhiteSpace(_options.WebhookSecret) ? null : _options.WebhookSecret;

            await bot.SetWebhookAsync(url, secret, cancellationToken);
            _logger.LogInformation("Webhook бота зарегистрирован: {Url}", url);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "Не удалось зарегистрировать webhook бота");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
