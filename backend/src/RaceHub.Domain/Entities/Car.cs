namespace RaceHub.Domain.Entities;
public class Car : BaseEntity
{
    public string Name { get; private set; } = null!;

    public decimal TopSpeed { get; private set; }

    public decimal Acceleration { get; private set; }

    public decimal Handling { get; private set; }

    public decimal Braking { get; private set; }

    public decimal NitroCapacity { get; private set; }

    /// <summary>
    /// Coins required to buy this car (see UserCar for the ownership
    /// record a purchase creates). 0 means it's a starter car — every
    /// user can equip it without ever needing a UserCar row, so the
    /// original catalog doesn't need retroactive ownership rows seeded
    /// for every existing account.
    /// </summary>
    public decimal Price { get; private set; }

    public bool IsActive { get; private set; }

    private Car() { }

    public Car(
        string name,
        decimal topSpeed,
        decimal acceleration,
        decimal handling,
        decimal braking,
        decimal nitroCapacity,
        decimal price = 0)
    {
        Name = name;
        TopSpeed = topSpeed;
        Acceleration = acceleration;
        Handling = handling;
        Braking = braking;
        NitroCapacity = nitroCapacity;
        Price = price;
        IsActive = true;
    }

    /// <summary>
    /// Only exists so Program.cs's catalog-sync seed can backfill/adjust
    /// Price on cars that were already seeded before pricing existed (or
    /// when tuning prices later) — mirrors the same "sync, don't just
    /// insert-if-missing" approach already used for track geometry.
    /// </summary>
    public void SetPrice(decimal price) => Price = price;

    /// <summary>
    /// Catalog-sync companion to SetPrice: applies a full stat rebalance to
    /// an already-seeded car. Used when the economy is retuned (e.g. making
    /// stats scale with price) so existing databases pick up the new curve
    /// without a reseed.
    /// </summary>
    public void SetStats(decimal topSpeed, decimal acceleration, decimal handling, decimal braking, decimal nitroCapacity)
    {
        TopSpeed = topSpeed;
        Acceleration = acceleration;
        Handling = handling;
        Braking = braking;
        NitroCapacity = nitroCapacity;
    }
}
