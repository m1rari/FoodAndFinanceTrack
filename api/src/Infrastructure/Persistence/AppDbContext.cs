using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceFoodTracker.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext, IApplicationDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ReceiptItem> ReceiptItems => Set<ReceiptItem>();
    public DbSet<FoodLog> FoodLogs => Set<FoodLog>();
    public DbSet<SavedDish> SavedDishes => Set<SavedDish>();
    public DbSet<Statement> Statements => Set<Statement>();
    public DbSet<CategoryRule> CategoryRules => Set<CategoryRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
