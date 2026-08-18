using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using RaceHub.Application.Interfaces.Authentication;

namespace RaceHub.Infrastructure.Authentication;

public class GoogleAuthService : IGoogleAuthService
{
    private readonly string _clientId;

    public GoogleAuthService(IConfiguration configuration)
    {
        _clientId = configuration["Authentication:Google:ClientId"]
            ?? throw new InvalidOperationException("Authentication:Google:ClientId is not configured.");
    }

    public async Task<GoogleUserPayload?> ValidateIdTokenAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _clientId }
            };

            // Validates the signature against Google's public keys, the
            // issuer, the expiry, and that the token was issued for our
            // Client ID — everything needed to trust payload.Email/Subject.
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

            return new GoogleUserPayload(
                payload.Email,
                payload.Name,
                payload.Subject,
                payload.EmailVerified);
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}
