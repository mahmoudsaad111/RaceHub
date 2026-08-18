/** Mirrors backend RaceHub.Application.DTOs.Authentication.AuthResponse. */
export interface AuthResponse {
  userId: string;
  email: string;
  displayName: string;
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
}

/** Mirrors backend RegisterCommand. */
export interface RegisterRequest {
  displayName: string;
  email: string;
  password: string;
}

/** Mirrors backend LoginCommand. */
export interface LoginRequest {
  email: string;
  password: string;
}

/** Mirrors backend GoogleLoginCommand. */
export interface GoogleLoginRequest {
  idToken: string;
}

/** Mirrors backend RefreshTokenCommand. */
export interface RefreshTokenRequest {
  refreshToken: string;
}

/** Mirrors backend RevokeTokenCommand. */
export interface RevokeTokenRequest {
  refreshToken: string;
}

/** Shape returned by GET /api/auth/me. */
export interface CurrentUser {
  userId: string;
  email: string;
  displayName: string;
}
