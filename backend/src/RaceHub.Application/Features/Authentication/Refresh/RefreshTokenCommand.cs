using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Authentication;

namespace RaceHub.Application.Features.Authentication.Refresh;

public record RefreshTokenCommand(string RefreshToken) : IRequest<Result<AuthResponse>>;
