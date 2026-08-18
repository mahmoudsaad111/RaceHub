using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.Interfaces.Authentication;
using RaceHub.Application.Interfaces.Persistence;

namespace RaceHub.Application.Features.Authentication.Logout;

public class RevokeTokenCommandHandler : IRequestHandler<RevokeTokenCommand, Result>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _unitOfWork;

    public RevokeTokenCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        ITokenService tokenService,
        IUnitOfWork unitOfWork)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(RevokeTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = _tokenService.HashToken(request.RefreshToken);

        var existingToken = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

        // Logout is idempotent: whether the token was unknown, already
        // revoked, or valid, the client should always see a clean success —
        // never leak whether a given refresh token exists.
        if (existingToken is not null && existingToken.IsActive)
        {
            existingToken.Revoke();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
