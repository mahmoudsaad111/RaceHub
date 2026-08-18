using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaceHub.Domain.Entities;

namespace RaceHub.Infrastructure.Persistence.Configurations;

public class FriendshipConfiguration : IEntityTypeConfiguration<Friendship>
{
    public void Configure(EntityTypeBuilder<Friendship> builder)
    {
        builder.ToTable("Friendships");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.RespondedAtUtc);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        // Both FKs point at User (self-referencing, two directions) — both
        // must be Restrict, since SQL Server rejects multiple cascade paths
        // into the same table from the same foreign table.
        builder.HasOne(x => x.Requester)
            .WithMany()
            .HasForeignKey(x => x.RequesterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Addressee)
            .WithMany()
            .HasForeignKey(x => x.AddresseeId)
            .OnDelete(DeleteBehavior.Restrict);

        // One relationship (in either direction) per pair of users.
        builder.HasIndex(x => new { x.RequesterId, x.AddresseeId })
            .IsUnique();

        builder.HasIndex(x => x.AddresseeId);
    }
}
