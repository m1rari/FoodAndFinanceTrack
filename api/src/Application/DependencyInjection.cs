using FinanceFoodTracker.Application.Categories;
using FinanceFoodTracker.Application.Reports;
using FinanceFoodTracker.Application.Transactions;
using FinanceFoodTracker.Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceFoodTracker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IReportService, ReportService>();

        return services;
    }
}
