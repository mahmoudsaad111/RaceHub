using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaceHub.Domain.Entities;

namespace RaceHub.Infrastructure.Persistence.Configurations;
public class PlayerStatisticsConfiguration : IEntityTypeConfiguration<PlayerStatistics>
{
    public void Configure(EntityTypeBuilder<PlayerStatistics> builder)
    {
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.RatingPoints).HasDefaultValue(1000);
        builder.HasOne<User>().WithOne().HasForeignKey<PlayerStatistics>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
