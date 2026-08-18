using MediatR;
using Microsoft.AspNetCore.Identity;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Authentication;
using RaceHub.Application.Interfaces.Authentication;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Domain.Entities;

namespace RaceHub.Application.Features.Authentication.Refresh;

public class RefreshTokenCommandHandler
    : IRequestHandler<RefreshTokenCommand, Result<AuthResponse>>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly UserManager<User> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IAuthTokenIssuer _tokenIssuer;
    private readonly IUnitOfWork _unitOfWork;

    public RefreshTokenCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        UserManager<User> userManager,
        ITokenService tokenService,
        IAuthTokenIssuer tokenIssuer,
        IUnitOfWork unitOfWork)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _userManager = userManager;
        _tokenService = tokenService;
        _tokenIssuer = tokenIssuer;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AuthResponse>> Handle(
        RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        var tokenHash = _tokenService.HashToken(request.RefreshToken);

        var existingToken = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (existingToken is null || !existingToken.IsActive)
        {
            return Result<AuthResponse>.Failure("Invalid or expired refresh token.", "invalid_refresh_token");
        }

        var user = await _userManager.FindByIdAsync(existingToken.UserId.ToString());

        if (user is null)
        {
            return Result<AuthResponse>.Failure("Invalid or expired refresh token.", "invalid_refresh_token");
        }

        // Rotation: the old refresh token is revoked as soon as it's used,
        // and a brand new access/refresh pair is issued. If a stolen token
        // is ever replayed after the legitimate client already rotated it,
        // it will already be inactive here.
        existingToken.Revoke();

        var response = await _tokenIssuer.IssueAsync(user, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<AuthResponse>.Success(response);
    }
}
