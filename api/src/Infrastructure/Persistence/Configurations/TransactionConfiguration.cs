using FinanceFoodTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceFoodTracker.Infrastructure.Persistence.Configurations;

public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(t => t.AccountId).HasColumnName("account_id").IsRequired();
        builder.Property(t => t.CategoryId).HasColumnName("category_id");
        builder.Property(t => t.Type).HasColumnName("type").HasConversion<int>().IsRequired();
        builder.Property(t => t.Amount).HasColumnName("amount").HasPrecision(12, 2).IsRequired();
        builder.Property(t => t.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        builder.Property(t => t.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(t => t.Source).HasColumnName("source").HasConversion<int>().IsRequired();
        builder.Property(t => t.Comment).HasColumnName("comment");
        builder.Property(t => t.ReceiptId).HasColumnName("receipt_id");
        builder.Property(t => t.IsTransfer).HasColumnName("is_transfer").HasDefaultValue(false).IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(t => new { t.UserId, t.OccurredAt });

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Account)
            .WithMany()
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Category)
            .WithMany()
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(t => t.Receipt)
            .WithMany()
            .HasForeignKey(t => t.ReceiptId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
