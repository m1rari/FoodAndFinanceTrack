using FinanceFoodTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceFoodTracker.Infrastructure.Persistence.Configurations;

public sealed class FoodLogConfiguration : IEntityTypeConfiguration<FoodLog>
{
    public void Configure(EntityTypeBuilder<FoodLog> builder)
    {
        builder.ToTable("food_logs");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id");
        builder.Property(f => f.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(f => f.ImagePath).HasColumnName("image_path").IsRequired();
        builder.Property(f => f.DishName).HasColumnName("dish_name").HasMaxLength(500);
        builder.Property(f => f.UserContext).HasColumnName("user_context").HasMaxLength(1000);
        builder.Property(f => f.CaloriesMin).HasColumnName("calories_min").HasPrecision(10, 2);
        builder.Property(f => f.CaloriesMax).HasColumnName("calories_max").HasPrecision(10, 2);
        builder.Property(f => f.ProteinG).HasColumnName("protein_g").HasPrecision(10, 2);
        builder.Property(f => f.FatG).HasColumnName("fat_g").HasPrecision(10, 2);
        builder.Property(f => f.CarbsG).HasColumnName("carbs_g").HasPrecision(10, 2);
        builder.Property(f => f.ProteinMinG).HasColumnName("protein_min_g").HasPrecision(10, 2);
        builder.Property(f => f.ProteinMaxG).HasColumnName("protein_max_g").HasPrecision(10, 2);
        builder.Property(f => f.FatMinG).HasColumnName("fat_min_g").HasPrecision(10, 2);
        builder.Property(f => f.FatMaxG).HasColumnName("fat_max_g").HasPrecision(10, 2);
        builder.Property(f => f.CarbsMinG).HasColumnName("carbs_min_g").HasPrecision(10, 2);
        builder.Property(f => f.CarbsMaxG).HasColumnName("carbs_max_g").HasPrecision(10, 2);
        builder.Property(f => f.EatenAt).HasColumnName("eaten_at").IsRequired();
        builder.Property(f => f.AiRawResponse).HasColumnName("ai_raw_response").HasColumnType("jsonb");
        builder.Property(f => f.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(f => f.TelegramChatId).HasColumnName("telegram_chat_id");
        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne(f => f.User)
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
