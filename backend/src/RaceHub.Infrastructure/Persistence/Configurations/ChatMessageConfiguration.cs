using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaceHub.Domain.Entities;

namespace RaceHub.Infrastructure.Persistence.Configurations;

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.Property(x => x.ConversationId)
            .IsRequired();

        builder.Property(x => x.SenderId)
            .IsRequired();

        builder.Property(x => x.Content)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.SentAtUtc)
            .IsRequired();

        builder.HasIndex(x => new { x.ConversationId, x.SentAtUtc });
    }
}
