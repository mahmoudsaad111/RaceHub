using MediatR;
using RaceHub.Application.Common;

namespace RaceHub.Application.Features.Authentication.Logout;

public record RevokeTokenCommand(string RefreshToken) : IRequest<Result>;
