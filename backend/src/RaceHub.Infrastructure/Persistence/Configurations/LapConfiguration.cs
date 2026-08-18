using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaceHub.Domain.Entities;

namespace RaceHub.Infrastructure.Persistence.Configurations;

public class LapConfiguration : IEntityTypeConfiguration<Lap>
{
    public void Configure(EntityTypeBuilder<Lap> builder)
    {
        builder.ToTable("Laps");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.LapNumber)
            .IsRequired();

        builder.Property(x => x.LapTime)
            .IsRequired();

        builder.Property(x => x.CompletedAtUtc)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.HasOne(x => x.RacePlayer)
            .WithMany(x => x.Laps)
            .HasForeignKey(x => x.RacePlayerId)
            .OnDelete(DeleteBehavior.Cascade);

        // One player cannot have two records for the same lap.
        builder.HasIndex(x => new
        {
            x.RacePlayerId,
            x.LapNumber
        })
        .IsUnique();
    }
}