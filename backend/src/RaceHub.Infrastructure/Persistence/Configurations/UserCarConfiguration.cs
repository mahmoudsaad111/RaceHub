using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaceHub.Domain.Entities;

namespace RaceHub.Infrastructure.Persistence.Configurations;

public class UserCarConfiguration : IEntityTypeConfiguration<UserCar>
{
    public void Configure(EntityTypeBuilder<UserCar> builder)
    {
        builder.ToTable("UserCars");

        // (UserId, CarId) is naturally unique and IS the primary key —
        // no surrogate Id, same pattern as ProcessedMessage.
        builder.HasKey(x => new { x.UserId, x.CarId });

        builder.Property(x => x.PurchasedAtUtc).IsRequired();

        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Car>().WithMany().HasForeignKey(x => x.CarId).OnDelete(DeleteBehavior.Restrict);
    }
}
