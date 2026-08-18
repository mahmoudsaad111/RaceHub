using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaceHub.Domain.Entities;

namespace RaceHub.Infrastructure.Persistence.Configurations;
public class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        // ConsumerName needs an explicit max length: it's part of the
        // primary key, and SQL Server rejects nvarchar(max) (EF's default
        // for an unconfigured string column) as a key column outright —
        // table creation would fail. 100 is generously more than any
        // consumer's nameof(...) class name needs.
        builder.Property(x => x.ConsumerName).HasMaxLength(100);
        builder.HasKey(x => new { x.MessageId, x.ConsumerName });
    }
}