using FinanceFoodTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceFoodTracker.Infrastructure.Persistence.Configurations;

public sealed class ReceiptItemConfiguration : IEntityTypeConfiguration<ReceiptItem>
{
    public void Configure(EntityTypeBuilder<ReceiptItem> builder)
    {
        builder.ToTable("receipt_items");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.ReceiptId).HasColumnName("receipt_id").IsRequired();
        builder.Property(i => i.Name).HasColumnName("name").HasMaxLength(500).IsRequired();
        builder.Property(i => i.Quantity).HasColumnName("quantity").HasPrecision(12, 3);
        builder.Property(i => i.UnitPrice).HasColumnName("unit_price").HasPrecision(12, 2);
        builder.Property(i => i.TotalPrice).HasColumnName("total_price").HasPrecision(12, 2);
        builder.Property(i => i.CategoryId).HasColumnName("category_id");
        builder.Property(i => i.Confidence).HasColumnName("confidence").HasPrecision(5, 4);

        builder.HasOne(i => i.Receipt)
            .WithMany(r => r.Items)
            .HasForeignKey(i => i.ReceiptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Category)
            .WithMany()
            .HasForeignKey(i => i.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
