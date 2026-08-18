using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaceHub.Domain.Entities;

namespace RaceHub.Infrastructure.Persistence.Configurations;

public class RaceHistoryEntryConfiguration : IEntityTypeConfiguration<RaceHistoryEntry>
{
    public void Configure(EntityTypeBuilder<RaceHistoryEntry> builder)
    {
        builder.ToTable("RaceHistoryEntries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.RaceId).IsRequired();
        builder.Property(x => x.TrackId).IsRequired();
        builder.Property(x => x.Position).IsRequired();
        builder.Property(x => x.FinishTimeMs).IsRequired();
        builder.Property(x => x.RecordedAtUtc).IsRequired();

        // No navigation properties (User/Race/Track) on RaceHistoryEntry
        // itself — it's a denormalized read-model row for a "recent
        // races" feed, not a relational entity other aggregates navigate
        // through. Still enforce the FKs at the DB level via bare
        // HasOne<T>() (same pattern RaceConfiguration uses for
        // Race.HostUserId), so referential integrity holds without
        // forcing unwanted navigation properties onto User/Race/Track.
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Race>().WithMany().HasForeignKey(x => x.RaceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Track>().WithMany().HasForeignKey(x => x.TrackId).OnDelete(DeleteBehavior.Restrict);

        // "Recent races for this user" is the only query this table
        // exists to serve — index for exactly that access pattern.
        builder.HasIndex(x => new { x.UserId, x.RecordedAtUtc });
    }
}