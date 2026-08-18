using RaceHub.Domain.Entities;

namespace RaceHub.Application.Interfaces.Authentication;

public interface ITokenService
{
    string GenerateAccessToken(User user);

    string GenerateRefreshToken();

    /// <summary>
    /// Hashes a raw refresh token before it is persisted, so a stolen database
    /// never exposes usable refresh tokens.
    /// </summary>
    string HashToken(string token);

    DateTime GetAccessTokenExpiration();

    DateTime GetRefreshTokenExpiration();
}
