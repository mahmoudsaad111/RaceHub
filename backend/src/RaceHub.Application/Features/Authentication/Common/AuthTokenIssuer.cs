using RaceHub.Application.DTOs.Authentication;
using RaceHub.Application.Interfaces.Authentication;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Domain.Entities;

namespace RaceHub.Application.Features.Authentication.Common;

public class AuthTokenIssuer : IAuthTokenIssuer
{
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AuthTokenIssuer(
        ITokenService tokenService,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork)
    {
        _tokenService = tokenService;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<AuthResponse> IssueAsync(User user, CancellationToken cancellationToken = default)
    {
        var accessToken = _tokenService.GenerateAccessToken(user);
        var accessTokenExpiresAt = _tokenService.GetAccessTokenExpiration();

        var refreshToken = _tokenService.GenerateRefreshToken();
        var refreshTokenHash = _tokenService.HashToken(refreshToken);
        var refreshTokenExpiresAt = _tokenService.GetRefreshTokenExpiration();

        var refreshTokenEntity = new RefreshToken(
            user.Id,
            refreshTokenHash,
            refreshTokenExpiresAt);

        await _refreshTokenRepository.AddAsync(refreshTokenEntity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthResponse(
            user.Id,
            user.Email!,
            user.DisplayName,
            accessToken,
            accessTokenExpiresAt,
            refreshToken,
            refreshTokenExpiresAt);
    }
}
