namespace RaceHub.Domain.Entities;
public abstract class AuditableEntity : BaseEntity
{
    public DateTime? UpdatedAtUtc { get; protected set; }
}