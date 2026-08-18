using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaceHub.Domain.Entities;

namespace RaceHub.Infrastructure.Persistence.Configurations;

public class RaceResultConfiguration
    : IEntityTypeConfiguration<RaceResult>
{
    public void Configure(EntityTypeBuilder<RaceResult> builder)
    {
        builder.ToTable("RaceResults");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FinishingPosition)
            .IsRequired();

        builder.Property(x => x.TotalRaceTime)
            .IsRequired();

        builder.Property(x => x.BestLapTime);

        builder.Property(x => x.ExperienceEarned)
            .IsRequired();

        builder.Property(x => x.CoinsEarned)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.HasOne(x => x.Race)
            .WithMany(x => x.Results)
            .HasForeignKey(x => x.RaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany(x => x.RaceResults)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // A player gets one final result for a race.
        builder.HasIndex(x => new { x.RaceId, x.UserId })
            .IsUnique();

        builder.HasIndex(x => new
        {
            x.RaceId,
            x.FinishingPosition
        });
    }
}