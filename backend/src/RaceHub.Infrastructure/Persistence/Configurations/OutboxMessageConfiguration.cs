// RaceHub.Infrastructure/Persistence/Configurations/OutboxMessageConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaceHub.Domain.Entities;

namespace RaceHub.Infrastructure.Persistence.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Payload).IsRequired();

        // The publisher's poll is WHERE ProcessedOnUtc IS NULL ORDER BY
        // OccurredOnUtc — a composite index covers both the filter and the
        // sort in one lookup instead of filtering, then sorting separately.
        builder.HasIndex(x => new { x.ProcessedOnUtc, x.OccurredOnUtc });
    }
}