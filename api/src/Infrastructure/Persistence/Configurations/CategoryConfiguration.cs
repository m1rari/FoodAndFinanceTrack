using FinanceFoodTracker.Domain.Entities;
using FinanceFoodTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceFoodTracker.Infrastructure.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.UserId).HasColumnName("user_id");
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(c => c.Type).HasColumnName("type").HasConversion<int>().IsRequired();
        builder.Property(c => c.ParentId).HasColumnName("parent_id");
        builder.Property(c => c.IsSystem).HasColumnName("is_system").IsRequired();

        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Parent)
            .WithMany()
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(SystemCategories());
    }

    private static IEnumerable<Category> SystemCategories() => new List<Category>
    {
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111101"), Name = "Продукты", Type = TransactionType.Expense, IsSystem = true },
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111102"), Name = "Транспорт", Type = TransactionType.Expense, IsSystem = true },
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111103"), Name = "Жильё", Type = TransactionType.Expense, IsSystem = true },
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111104"), Name = "Здоровье", Type = TransactionType.Expense, IsSystem = true },
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111105"), Name = "Развлечения", Type = TransactionType.Expense, IsSystem = true },
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111106"), Name = "Одежда", Type = TransactionType.Expense, IsSystem = true },
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111107"), Name = "Связь", Type = TransactionType.Expense, IsSystem = true },
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111108"), Name = "Прочее", Type = TransactionType.Expense, IsSystem = true },
        new() { Id = Guid.Parse("22222222-2222-2222-2222-222222222201"), Name = "Зарплата", Type = TransactionType.Income, IsSystem = true },
        new() { Id = Guid.Parse("22222222-2222-2222-2222-222222222202"), Name = "Подработка", Type = TransactionType.Income, IsSystem = true },
        new() { Id = Guid.Parse("22222222-2222-2222-2222-222222222203"), Name = "Прочее", Type = TransactionType.Income, IsSystem = true }
    };
}
