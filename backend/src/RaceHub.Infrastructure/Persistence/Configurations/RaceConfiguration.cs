using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaceHub.Domain.Entities;

namespace RaceHub.Infrastructure.Persistence.Configurations;

public class RaceConfiguration : IEntityTypeConfiguration<Race>
{
    public void Configure(EntityTypeBuilder<Race> builder)
    {
        builder.ToTable("Races");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.HostUserId)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.MaxPlayers)
            .IsRequired();

        builder.Property(x => x.TotalLaps)
            .IsRequired();

        builder.Property(x => x.StartedAtUtc);

        builder.Property(x => x.FinishedAtUtc);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.HasOne(x => x.Track)
            .WithMany(x => x.Races)
            .HasForeignKey(x => x.TrackId)
            .OnDelete(DeleteBehavior.Restrict);

        // No navigation property back to User for the host — we only ever
        // need the id (e.g. "is the current user allowed to start this
        // race"), so a bare FK avoids an unused ICollection<Race> on User.
        builder.HasOne<Domain.Entities.User>()
            .WithMany()
            .HasForeignKey(x => x.HostUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Status);

        builder.HasMany(x => x.Players)
            .WithOne(x => x.Race)
            .HasForeignKey(x => x.RaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Results)
            .WithOne(x => x.Race)
            .HasForeignKey(x => x.RaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}