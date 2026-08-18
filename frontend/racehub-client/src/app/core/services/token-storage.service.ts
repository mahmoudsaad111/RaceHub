import { Injectable } from '@angular/core';
import { AuthResponse } from '../models/auth.models';

const ACCESS_TOKEN_KEY = 'rh_access_token';
const REFRESH_TOKEN_KEY = 'rh_refresh_token';
const ACCESS_TOKEN_EXPIRY_KEY = 'rh_access_token_expiry';
const USER_KEY = 'rh_user';

export interface StoredUser {
  userId: string;
  email: string;
  displayName: string;
}

/**
 * Thin wrapper around localStorage for auth state. Kept isolated behind
 * this service (rather than sprinkling localStorage calls through
 * AuthService/interceptor/guards) so the storage strategy can change later
 * — e.g. moving the refresh token to an httpOnly cookie set by the backend
 * — by editing one file.
 */
@Injectable({ providedIn: 'root' })
export class TokenStorageService {
  saveSession(auth: AuthResponse): void {
    localStorage.setItem(ACCESS_TOKEN_KEY, auth.accessToken);
    localStorage.setItem(REFRESH_TOKEN_KEY, auth.refreshToken);
    localStorage.setItem(ACCESS_TOKEN_EXPIRY_KEY, auth.accessTokenExpiresAtUtc);
    localStorage.setItem(
      USER_KEY,
      JSON.stringify({
        userId: auth.userId,
        email: auth.email,
        displayName: auth.displayName,
      } satisfies StoredUser),
    );
  }

  getAccessToken(): string | null {
    return localStorage.getItem(ACCESS_TOKEN_KEY);
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(REFRESH_TOKEN_KEY);
  }

  getUser(): StoredUser | null {
    const raw = localStorage.getItem(USER_KEY);
    return raw ? (JSON.parse(raw) as StoredUser) : null;
  }

  isAccessTokenExpired(): boolean {
    const expiry = localStorage.getItem(ACCESS_TOKEN_EXPIRY_KEY);
    if (!expiry) return true;
    // Small buffer so we refresh slightly before actual expiry.
    return Date.now() >= new Date(expiry).getTime() - 5_000;
  }

  hasSession(): boolean {
    return !!this.getAccessToken() && !!this.getRefreshToken();
  }

  clear(): void {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(ACCESS_TOKEN_EXPIRY_KEY);
    localStorage.removeItem(USER_KEY);
  }
}
