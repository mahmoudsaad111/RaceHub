using MediatR;
using Microsoft.AspNetCore.Identity;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Authentication;
using RaceHub.Application.Interfaces.Authentication;
using RaceHub.Domain.Entities;

namespace RaceHub.Application.Features.Authentication.GoogleLogin;

public class GoogleLoginCommandHandler
    : IRequestHandler<GoogleLoginCommand, Result<AuthResponse>>
{
    private const string ProviderName = "Google";

    private readonly UserManager<User> _userManager;
    private readonly IGoogleAuthService _googleAuthService;
    private readonly IAuthTokenIssuer _tokenIssuer;

    public GoogleLoginCommandHandler(
        UserManager<User> userManager,
        IGoogleAuthService googleAuthService,
        IAuthTokenIssuer tokenIssuer)
    {
        _userManager = userManager;
        _googleAuthService = googleAuthService;
        _tokenIssuer = tokenIssuer;
    }

    public async Task<Result<AuthResponse>> Handle(
        GoogleLoginCommand request,
        CancellationToken cancellationToken)
    {
        var payload = await _googleAuthService.ValidateIdTokenAsync(request.IdToken, cancellationToken);

        if (payload is null || !payload.EmailVerified)
        {
            return Result<AuthResponse>.Failure("Invalid Google token.", "invalid_google_token");
        }

        var loginInfo = new UserLoginInfo(ProviderName, payload.Subject, ProviderName);

        // AspNetUserLogins (part of ASP.NET Identity) is exactly the
        // repository we need here: it maps an external provider + subject id
        // to one of our users, so the same Google account always resolves to
        // the same RaceHub user even if they change their display name.
        var user = await _userManager.FindByLoginAsync(loginInfo.LoginProvider, loginInfo.ProviderKey);

        if (user is null)
        {
            // No linked login yet — fall back to matching by email so a user
            // who registered with email/password and later clicks
            // "Continue with Google" lands on the same account.
            user = await _userManager.FindByEmailAsync(payload.Email);

            if (user is null)
            {
                user = new User
                {
                    UserName = payload.Email,
                    Email = payload.Email,
                    DisplayName = payload.Name,
                    EmailConfirmed = true
                };

                var createResult = await _userManager.CreateAsync(user);

                if (!createResult.Succeeded)
                {
                    var error = string.Join(" ", createResult.Errors.Select(e => e.Description));
                    return Result<AuthResponse>.Failure(error, "registration_failed");
                }
            }

            var addLoginResult = await _userManager.AddLoginAsync(user, loginInfo);

            if (!addLoginResult.Succeeded)
            {
                var error = string.Join(" ", addLoginResult.Errors.Select(e => e.Description));
                return Result<AuthResponse>.Failure(error, "google_link_failed");
            }
        }

        var response = await _tokenIssuer.IssueAsync(user, cancellationToken);

        return Result<AuthResponse>.Success(response);
    }
}
