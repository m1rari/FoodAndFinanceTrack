using FinanceFoodTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceFoodTracker.Infrastructure.Persistence.Configurations;

public sealed class SavedDishConfiguration : IEntityTypeConfiguration<SavedDish>
{
    public void Configure(EntityTypeBuilder<SavedDish> builder)
    {
        builder.ToTable("saved_dishes");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(d => d.Name).HasColumnName("name").HasMaxLength(500).IsRequired();
        builder.Property(d => d.NameKey).HasColumnName("name_key").HasMaxLength(500).IsRequired();
        builder.Property(d => d.CaloriesMin).HasColumnName("calories_min").HasPrecision(10, 2);
        builder.Property(d => d.CaloriesMax).HasColumnName("calories_max").HasPrecision(10, 2);
        builder.Property(d => d.ProteinMinG).HasColumnName("protein_min_g").HasPrecision(10, 2);
        builder.Property(d => d.ProteinMaxG).HasColumnName("protein_max_g").HasPrecision(10, 2);
        builder.Property(d => d.FatMinG).HasColumnName("fat_min_g").HasPrecision(10, 2);
        builder.Property(d => d.FatMaxG).HasColumnName("fat_max_g").HasPrecision(10, 2);
        builder.Property(d => d.CarbsMinG).HasColumnName("carbs_min_g").HasPrecision(10, 2);
        builder.Property(d => d.CarbsMaxG).HasColumnName("carbs_max_g").HasPrecision(10, 2);
        builder.Property(d => d.IsFavorite).HasColumnName("is_favorite").IsRequired();
        builder.Property(d => d.UseCount).HasColumnName("use_count").IsRequired();
        builder.Property(d => d.LastUsedAt).HasColumnName("last_used_at").IsRequired();
        builder.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(d => new { d.UserId, d.NameKey }).IsUnique();

        builder.HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
