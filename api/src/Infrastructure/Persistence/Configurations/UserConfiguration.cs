using FinanceFoodTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceFoodTracker.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.TelegramId).HasColumnName("telegram_id").IsRequired();
        builder.HasIndex(u => u.TelegramId).IsUnique();
        builder.Property(u => u.Username).HasColumnName("username").HasMaxLength(255);
        builder.Property(u => u.CreatedAt).HasColumnName("created_at").IsRequired();
    }
}
