using FinanceFoodTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceFoodTracker.Infrastructure.Persistence.Configurations;

public sealed class StatementConfiguration : IEntityTypeConfiguration<Statement>
{
    public void Configure(EntityTypeBuilder<Statement> builder)
    {
        builder.ToTable("statements");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(s => s.FileName).HasColumnName("file_name").HasMaxLength(500).IsRequired();
        builder.Property(s => s.PdfPath).HasColumnName("pdf_path").IsRequired();
        builder.Property(s => s.RawText).HasColumnName("raw_text");
        builder.Property(s => s.ParsedOperations).HasColumnName("parsed_operations").HasColumnType("jsonb");
        builder.Property(s => s.AiRawResponse).HasColumnName("ai_raw_response").HasColumnType("jsonb");
        builder.Property(s => s.Error).HasColumnName("error").HasMaxLength(1000);
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(s => s.ConfirmedAt).HasColumnName("confirmed_at");
        builder.Property(s => s.CreatedCount).HasColumnName("created_count").IsRequired();
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
