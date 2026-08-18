using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaceHub.Domain.Entities;

namespace RaceHub.Infrastructure.Persistence.Configurations;

public class TrackCheckpointConfiguration
    : IEntityTypeConfiguration<TrackCheckpoint>
{
    public void Configure(EntityTypeBuilder<TrackCheckpoint> builder)
    {
        builder.ToTable("TrackCheckpoints");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Sequence)
            .IsRequired();

        builder.Property(x => x.PositionX)
            .HasPrecision(10, 3)
            .IsRequired();

        builder.Property(x => x.PositionY)
            .HasPrecision(10, 3)
            .IsRequired();

        builder.Property(x => x.Width)
            .HasPrecision(10, 3)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.HasOne(x => x.Track)
            .WithMany(x => x.Checkpoints)
            .HasForeignKey(x => x.TrackId)
            .OnDelete(DeleteBehavior.Cascade);

        // A track cannot have two checkpoints with the same sequence.
        builder.HasIndex(x => new { x.TrackId, x.Sequence })
            .IsUnique();
    }
}