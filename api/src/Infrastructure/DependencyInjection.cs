using System.Net.Http.Headers;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Common.Options;
using FinanceFoodTracker.Application.FoodLogs;
using FinanceFoodTracker.Application.FoodLogs.Analysis;
using FinanceFoodTracker.Application.Receipts;
using FinanceFoodTracker.Application.Receipts.Analysis;
using FinanceFoodTracker.Application.Statements.Analysis;
using FinanceFoodTracker.Infrastructure.Analysis;
using FinanceFoodTracker.Infrastructure.Pdf;
using FinanceFoodTracker.Infrastructure.Persistence;
using FinanceFoodTracker.Infrastructure.Security;
using FinanceFoodTracker.Infrastructure.Storage;
using FinanceFoodTracker.Infrastructure.Telegram;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FinanceFoodTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Строка подключения 'Default' не задана.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();

        services.Configure<TelegramOptions>(configuration.GetSection(TelegramOptions.SectionName));
        services.AddSingleton<ITelegramInitDataValidator, TelegramInitDataValidator>();

        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        services.Configure<OpenCodeGoOptions>(configuration.GetSection(OpenCodeGoOptions.SectionName));

        services.AddHttpClient<OpenCodeGoVisionClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<OpenCodeGoOptions>>().Value;
            var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl)
                ? "https://opencode.ai/zen/go/v1"
                : options.BaseUrl;

            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FoodAndFinanceTracker/1.0");
        });

        services.AddSingleton<IPdfTextExtractor, PdfPigTextExtractor>();

        services.AddScoped<OpenCodeGoReceiptAnalyzer>();
        services.AddScoped<OpenCodeGoFoodImageAnalyzer>();
        services.AddScoped<OpenCodeGoStatementAnalyzer>();
        services.AddSingleton<StubReceiptAnalyzer>();
        services.AddSingleton<StubFoodImageAnalyzer>();
        services.AddSingleton<StubStatementAnalyzer>();

        services.AddScoped<IReceiptAnalyzer>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<OpenCodeGoOptions>>().Value;
            return string.IsNullOrWhiteSpace(options.ApiKey)
                ? provider.GetRequiredService<StubReceiptAnalyzer>()
                : provider.GetRequiredService<OpenCodeGoReceiptAnalyzer>();
        });

        services.AddScoped<IFoodImageAnalyzer>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<OpenCodeGoOptions>>().Value;
            return string.IsNullOrWhiteSpace(options.ApiKey)
                ? provider.GetRequiredService<StubFoodImageAnalyzer>()
                : provider.GetRequiredService<OpenCodeGoFoodImageAnalyzer>();
        });

        services.AddScoped<IStatementAnalyzer>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<OpenCodeGoOptions>>().Value;
            return string.IsNullOrWhiteSpace(options.ApiKey)
                ? provider.GetRequiredService<StubStatementAnalyzer>()
                : provider.GetRequiredService<OpenCodeGoStatementAnalyzer>();
        });

        services.AddSingleton<IReceiptProcessingQueue, ReceiptProcessingQueue>();
        services.AddHostedService<ReceiptProcessingWorker>();

        services.AddSingleton<IFoodLogProcessingQueue, FoodLogProcessingQueue>();
        services.AddHostedService<FoodLogProcessingWorker>();

        services.AddHttpClient<ITelegramBot, TelegramBotClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<TelegramOptions>>().Value;
            client.BaseAddress = new Uri($"https://api.telegram.org/bot{options.BotToken}/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddSingleton<IChatModeStore, InMemoryChatModeStore>();
        services.AddHostedService<TelegramWebhookSetup>();

        return services;
    }
}
