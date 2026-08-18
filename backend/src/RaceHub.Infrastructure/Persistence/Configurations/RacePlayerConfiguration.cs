using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaceHub.Domain.Entities;

namespace RaceHub.Infrastructure.Persistence.Configurations;

public class RacePlayerConfiguration
    : IEntityTypeConfiguration<RacePlayer>
{
    public void Configure(EntityTypeBuilder<RacePlayer> builder)
    {
        builder.ToTable("RacePlayers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.CurrentLap)
            .IsRequired();

        builder.Property(x => x.CurrentCheckpoint)
            .IsRequired();

        builder.Property(x => x.FinishingPosition);

        builder.Property(x => x.BestLapTime);

        builder.Property(x => x.TotalRaceTime);

        builder.Property(x => x.FinishedAtUtc);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        // Race -> RacePlayers
        builder.HasOne(x => x.Race)
            .WithMany(x => x.Players)
            .HasForeignKey(x => x.RaceId)
            .OnDelete(DeleteBehavior.Cascade);

        // User -> RaceParticipations
        builder.HasOne(x => x.User)
            .WithMany(x => x.RaceParticipations)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Car -> RacePlayers
        builder.HasOne(x => x.Car)
            .WithMany()
            .HasForeignKey(x => x.CarId)
            .OnDelete(DeleteBehavior.Restrict);

        // A player can only join a race once.
        builder.HasIndex(x => new { x.RaceId, x.UserId })
            .IsUnique();

        // Useful for querying all players in a race.
        builder.HasIndex(x => x.RaceId);

        // Useful for querying a player's race history.
        builder.HasIndex(x => x.UserId);
    }
}