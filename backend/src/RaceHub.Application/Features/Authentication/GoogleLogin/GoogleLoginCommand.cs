using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Authentication;

namespace RaceHub.Application.Features.Authentication.GoogleLogin;

/// <summary>
/// IdToken is the Google ID token obtained client-side (e.g. via Google
/// Identity Services in the Angular app), not an authorization code — the
/// API only ever verifies it, it never talks to Google directly for login.
/// </summary>
public record GoogleLoginCommand(string IdToken) : IRequest<Result<AuthResponse>>;
