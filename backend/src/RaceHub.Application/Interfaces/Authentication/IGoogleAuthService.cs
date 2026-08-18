namespace RaceHub.Application.Interfaces.Authentication;

public record GoogleUserPayload(
    string Email,
    string Name,
    string Subject,
    bool EmailVerified);

public interface IGoogleAuthService
{
    /// <summary>
    /// Validates a Google ID token (obtained client-side via Google Identity
    /// Services) against Google's public keys and our configured Client ID.
    /// Returns null if the token is invalid, expired, or was issued for a
    /// different client.
    /// </summary>
    Task<GoogleUserPayload?> ValidateIdTokenAsync(string idToken, CancellationToken cancellationToken = default);
}
