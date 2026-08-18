using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceHub.Application.Common;
using RaceHub.Application.Features.Authentication.GoogleLogin;
using RaceHub.Application.Features.Authentication.Login;
using RaceHub.Application.Features.Authentication.Logout;
using RaceHub.Application.Features.Authentication.Refresh;
using RaceHub.Application.Features.Authentication.Register;

namespace RaceHub.API.Controllers;

[Route("api/auth")]
public class AuthenticationController : ApiControllerBase
{
    private readonly ISender _sender;

    public AuthenticationController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);

        return HandleResult(result, "Account created successfully.");
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);

        return HandleResult(result, "Logged in successfully.");
    }

    /// <summary>
    /// Body: { "idToken": "..." } — the Google ID token obtained client-side
    /// via Google Identity Services (e.g. the Angular Google Sign-In button).
    /// </summary>
    [HttpPost("google")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleLogin(GoogleLoginCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);

        return HandleResult(result, "Logged in with Google successfully.");
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);

        return HandleResult(result, "Token refreshed successfully.");
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RevokeTokenCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);

        return HandleResult(result, "Logged out successfully.");
    }

    /// <summary>
    /// Reads the claims baked into the access token by TokenService.GenerateAccessToken.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var userId = User.FindFirst("userId")?.Value;
        var displayName = User.FindFirst("displayName")?.Value;
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

        return Ok(ApiResponse<object>.SuccessResponse(new { userId, email, displayName }));
    }
}
