namespace RaceHub.Application.DTOs.Cars;

public record CarDto(
    Guid Id,
    string Name,
    decimal TopSpeed,
    decimal Acceleration,
    decimal Handling,
    decimal Braking,
    decimal NitroCapacity,
    bool IsActive,
    decimal Price,
    bool Owned);
