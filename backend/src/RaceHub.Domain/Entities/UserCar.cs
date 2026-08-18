namespace RaceHub.Domain.Entities;

/// <summary>
/// Ownership record created when a user spends Coins on a car with a
/// non-zero Price (see Car.Price). No surrogate Id — (UserId, CarId) is
/// naturally unique and is the primary key itself (see
/// UserCarConfiguration), same pattern as ProcessedMessage.
/// </summary>
public class UserCar
{
    public Guid UserId { get; private set; }
    public Guid CarId { get; private set; }
    public DateTime PurchasedAtUtc { get; private set; }

    private UserCar() { }

    public UserCar(Guid userId, Guid carId)
    {
        UserId = userId;
        CarId = carId;
        PurchasedAtUtc = DateTime.UtcNow;
    }
}
