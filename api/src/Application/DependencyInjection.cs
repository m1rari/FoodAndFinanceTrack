using FinanceFoodTracker.Application.Categories;
using FinanceFoodTracker.Application.FoodLogs;
using FinanceFoodTracker.Application.Receipts;
using FinanceFoodTracker.Application.SavedDishes;
using FinanceFoodTracker.Application.Statements;
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
        services.AddScoped<IReceiptService, ReceiptService>();
        services.AddScoped<IReceiptProcessor, ReceiptProcessor>();
        services.AddScoped<IFoodLogService, FoodLogService>();
        services.AddScoped<IFoodLogProcessor, FoodLogProcessor>();
        services.AddScoped<ISavedDishService, SavedDishService>();
        services.AddScoped<IStatementService, StatementService>();
        services.AddScoped<IReportService, ReportService>();

        return services;
    }
}
