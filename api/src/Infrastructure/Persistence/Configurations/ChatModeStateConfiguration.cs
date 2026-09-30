using FinanceFoodTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceFoodTracker.Infrastructure.Persistence.Configurations;

public sealed class ChatModeStateConfiguration : IEntityTypeConfiguration<ChatModeState>
{
    public void Configure(EntityTypeBuilder<ChatModeState> builder)
    {
        builder.ToTable("chat_modes");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.ChatId).HasColumnName("chat_id").IsRequired();
        builder.Property(c => c.Mode).HasColumnName("mode").IsRequired();
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(c => c.ChatId).IsUnique();
    }
}
