using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaceHub.Domain.Entities;

namespace RaceHub.Infrastructure.Persistence.Configurations;

public class CarConfiguration : IEntityTypeConfiguration<Car>
{
    public void Configure(EntityTypeBuilder<Car> builder)
    {
        builder.ToTable("Cars");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.TopSpeed)
            .HasPrecision(8, 2)
            .IsRequired();

        builder.Property(x => x.Acceleration)
            .HasPrecision(8, 2)
            .IsRequired();

        builder.Property(x => x.Handling)
            .HasPrecision(8, 2)
            .IsRequired();

        builder.Property(x => x.Braking)
            .HasPrecision(8, 2)
            .IsRequired();

        builder.Property(x => x.NitroCapacity)
            .HasPrecision(8, 2)
            .IsRequired();

        builder.Property(x => x.Price)
            .HasPrecision(8, 2)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();
    }
}
