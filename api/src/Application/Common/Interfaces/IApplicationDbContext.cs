using FinanceFoodTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceFoodTracker.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Account> Accounts { get; }
    DbSet<Category> Categories { get; }
    DbSet<Transaction> Transactions { get; }
    DbSet<Receipt> Receipts { get; }
    DbSet<ReceiptItem> ReceiptItems { get; }
    DbSet<FoodLog> FoodLogs { get; }
    DbSet<CategoryRule> CategoryRules { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
