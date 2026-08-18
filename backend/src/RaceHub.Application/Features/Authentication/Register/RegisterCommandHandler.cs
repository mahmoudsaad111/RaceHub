using MediatR;
using Microsoft.AspNetCore.Identity;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Authentication;
using RaceHub.Application.Interfaces.Authentication;
using RaceHub.Domain.Entities;

namespace RaceHub.Application.Features.Authentication.Register;

public class RegisterCommandHandler
    : IRequestHandler<RegisterCommand, Result<AuthResponse>>
{
    private readonly UserManager<User> _userManager;
    private readonly IAuthTokenIssuer _tokenIssuer;

    public RegisterCommandHandler(UserManager<User> userManager, IAuthTokenIssuer tokenIssuer)
    {
        _userManager = userManager;
        _tokenIssuer = tokenIssuer;
    }

    public async Task<Result<AuthResponse>> Handle(
        RegisterCommand request,
        CancellationToken cancellationToken)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);

        if (existingUser is not null)
        {
            return Result<AuthResponse>.Failure(
                "An account with this email already exists.",
                "email_taken");
        }

        var user = new User
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName
        };

        // UserManager is Identity's own repository + unit-of-work abstraction
        // for user data: it hashes the password, enforces the password/user
        // rules configured in Infrastructure, and persists through the
        // EF Core store. Re-wrapping it in a custom IUserRepository would
        // just add an indirection layer around the same store without adding
        // value, so auth handlers talk to UserManager directly.
        var createResult = await _userManager.CreateAsync(user, request.Password);

        if (!createResult.Succeeded)
        {
            var error = string.Join(" ", createResult.Errors.Select(e => e.Description));
            return Result<AuthResponse>.Failure(error, "registration_failed");
        }

        var response = await _tokenIssuer.IssueAsync(user, cancellationToken);

        return Result<AuthResponse>.Success(response);
    }
}
