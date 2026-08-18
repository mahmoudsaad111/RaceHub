using MediatR;
using Microsoft.AspNetCore.Identity;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Authentication;
using RaceHub.Application.Interfaces.Authentication;
using RaceHub.Domain.Entities;

namespace RaceHub.Application.Features.Authentication.Login;

public class LoginCommandHandler
    : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    private readonly UserManager<User> _userManager;
    private readonly IAuthTokenIssuer _tokenIssuer;

    public LoginCommandHandler(UserManager<User> userManager, IAuthTokenIssuer tokenIssuer)
    {
        _userManager = userManager;
        _tokenIssuer = tokenIssuer;
    }

    public async Task<Result<AuthResponse>> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        // Same generic error whether the email doesn't exist, the account
        // has no password (Google-only account), or the password is wrong —
        // never reveal which case it was.
        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
        {
            return Result<AuthResponse>.Failure("Invalid email or password.", "invalid_credentials");
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);

        if (!passwordValid)
        {
            return Result<AuthResponse>.Failure("Invalid email or password.", "invalid_credentials");
        }

        var response = await _tokenIssuer.IssueAsync(user, cancellationToken);

        return Result<AuthResponse>.Success(response);
    }
}
