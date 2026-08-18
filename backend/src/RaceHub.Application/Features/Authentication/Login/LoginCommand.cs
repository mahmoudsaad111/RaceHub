using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Authentication;

namespace RaceHub.Application.Features.Authentication.Login;

public record LoginCommand(
    string Email,
    string Password) : IRequest<Result<AuthResponse>>;
