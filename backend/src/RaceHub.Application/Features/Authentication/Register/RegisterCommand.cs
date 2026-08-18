using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Authentication;

namespace RaceHub.Application.Features.Authentication.Register;

public record RegisterCommand(
    string DisplayName,
    string Email,
    string Password) : IRequest<Result<AuthResponse>>;
