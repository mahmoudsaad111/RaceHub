using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaceHub.Domain.Entities;

namespace RaceHub.Infrastructure.Persistence.Configurations;

public class UserAchievementConfiguration : IEntityTypeConfiguration<UserAchievement>
{
    public void Configure(EntityTypeBuilder<UserAchievement> builder)
    {
        builder.ToTable("UserAchievements");

        // One row per (user, achievement) — the database-level backstop for
        // "already unlocked". Id stays the PK (a user can unlock many, an
        // achievement can be unlocked by many), the unique index is what
        // actually enforces no-duplicates.
        builder.HasIndex(a => new { a.UserId, a.Key }).IsUnique();

        builder.Property(a => a.Key).HasMaxLength(100);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
