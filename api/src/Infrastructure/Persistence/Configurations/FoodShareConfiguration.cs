using FinanceFoodTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceFoodTracker.Infrastructure.Persistence.Configurations;

public sealed class FoodShareConfiguration : IEntityTypeConfiguration<FoodShare>
{
    public void Configure(EntityTypeBuilder<FoodShare> builder)
    {
        builder.ToTable("food_shares");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.Token).HasColumnName("token").HasMaxLength(32).IsRequired();
        builder.Property(s => s.OwnerUserId).HasColumnName("owner_user_id").IsRequired();
        builder.Property(s => s.DishName).HasColumnName("dish_name").HasMaxLength(500).IsRequired();
        builder.Property(s => s.UserContext).HasColumnName("user_context").HasMaxLength(1000);
        builder.Property(s => s.CaloriesMin).HasColumnName("calories_min").HasPrecision(10, 2);
        builder.Property(s => s.CaloriesMax).HasColumnName("calories_max").HasPrecision(10, 2);
        builder.Property(s => s.ProteinMinG).HasColumnName("protein_min_g").HasPrecision(10, 2);
        builder.Property(s => s.ProteinMaxG).HasColumnName("protein_max_g").HasPrecision(10, 2);
        builder.Property(s => s.FatMinG).HasColumnName("fat_min_g").HasPrecision(10, 2);
        builder.Property(s => s.FatMaxG).HasColumnName("fat_max_g").HasPrecision(10, 2);
        builder.Property(s => s.CarbsMinG).HasColumnName("carbs_min_g").HasPrecision(10, 2);
        builder.Property(s => s.CarbsMaxG).HasColumnName("carbs_max_g").HasPrecision(10, 2);
        builder.Property(s => s.ClaimCount).HasColumnName("claim_count").IsRequired();
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.ExpiresAt).HasColumnName("expires_at").IsRequired();

        builder.HasIndex(s => s.Token).IsUnique();

        builder.HasOne(s => s.Owner)
            .WithMany()
            .HasForeignKey(s => s.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
