using System.Net.Http.Headers;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Common.Options;
using FinanceFoodTracker.Application.Receipts;
using FinanceFoodTracker.Application.Receipts.Analysis;
using FinanceFoodTracker.Infrastructure.Analysis;
using FinanceFoodTracker.Infrastructure.Persistence;
using FinanceFoodTracker.Infrastructure.Security;
using FinanceFoodTracker.Infrastructure.Storage;
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

        services.AddHttpClient<OpenCodeGoReceiptAnalyzer>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<OpenCodeGoOptions>>().Value;
            var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl)
                ? "https://opencode.ai/zen/go/v1"
                : options.BaseUrl;

            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FoodAndFinanceTracker/1.0");
        });

        services.AddSingleton<StubReceiptAnalyzer>();
        services.AddScoped<IReceiptAnalyzer>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<OpenCodeGoOptions>>().Value;
            return string.IsNullOrWhiteSpace(options.ApiKey)
                ? provider.GetRequiredService<StubReceiptAnalyzer>()
                : provider.GetRequiredService<OpenCodeGoReceiptAnalyzer>();
        });

        services.AddSingleton<IReceiptProcessingQueue, ReceiptProcessingQueue>();
        services.AddHostedService<ReceiptProcessingWorker>();

        return services;
    }
}
