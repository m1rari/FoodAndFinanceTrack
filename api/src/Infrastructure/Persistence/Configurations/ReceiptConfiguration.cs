using FinanceFoodTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceFoodTracker.Infrastructure.Persistence.Configurations;

public sealed class ReceiptConfiguration : IEntityTypeConfiguration<Receipt>
{
    public void Configure(EntityTypeBuilder<Receipt> builder)
    {
        builder.ToTable("receipts");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(r => r.ImagePath).HasColumnName("image_path").IsRequired();
        builder.Property(r => r.MerchantName).HasColumnName("merchant_name").HasMaxLength(255);
        builder.Property(r => r.PurchaseDate).HasColumnName("purchase_date");
        builder.Property(r => r.TotalAmount).HasColumnName("total_amount").HasPrecision(12, 2);
        builder.Property(r => r.RawOcrText).HasColumnName("raw_ocr_text");
        builder.Property(r => r.AiRawResponse).HasColumnName("ai_raw_response").HasColumnType("jsonb");
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(r => r.Confidence).HasColumnName("confidence").HasPrecision(5, 4);
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
