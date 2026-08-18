using RaceHub.Application.DTOs.Authentication;
using RaceHub.Domain.Entities;

namespace RaceHub.Application.Interfaces.Authentication;

/// <summary>
/// Shared token-issuing logic used by every auth flow (register, login,
/// Google login, refresh) so each handler doesn't repeat the same
/// access-token + refresh-token creation code.
/// </summary>
public interface IAuthTokenIssuer
{
    Task<AuthResponse> IssueAsync(User user, CancellationToken cancellationToken = default);
}
