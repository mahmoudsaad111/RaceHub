namespace RaceHub.Application.DTOs.Authentication;

public record AuthResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);
