import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, map, of, tap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import {
  AuthResponse,
  CurrentUser,
  GoogleLoginRequest,
  LoginRequest,
  RegisterRequest,
} from '../models/auth.models';
import { SKIP_AUTH_REFRESH } from '../interceptors/auth.interceptor';
import { StoredUser, TokenStorageService } from './token-storage.service';

/** Normalized shape the UI works with for a failed call. */
export interface AuthError {
  message: string;
  errorCode?: string;
  fieldErrors?: Record<string, string[]>;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly tokenStorage = inject(TokenStorageService);

  private readonly baseUrl = `${environment.apiUrl}/auth`;

  /** Current user, or null when signed out. Seeded from localStorage on load. */
  private readonly _currentUser = signal<StoredUser | null>(this.tokenStorage.getUser());
  readonly currentUser = this._currentUser.asReadonly();
  readonly isAuthenticated = computed(() => this._currentUser() !== null);

  register(request: RegisterRequest): Observable<AuthResponse> {
    return this.http
      .post<ApiResponse<AuthResponse>>(`${this.baseUrl}/register`, request)
      .pipe(
        map((res) => this.onAuthSuccess(res)),
        catchError((err) => throwError(() => this.toAuthError(err))),
      );
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<ApiResponse<AuthResponse>>(`${this.baseUrl}/login`, request)
      .pipe(
        map((res) => this.onAuthSuccess(res)),
        catchError((err) => throwError(() => this.toAuthError(err))),
      );
  }

  loginWithGoogle(request: GoogleLoginRequest): Observable<AuthResponse> {
    return this.http
      .post<ApiResponse<AuthResponse>>(`${this.baseUrl}/google`, request)
      .pipe(
        map((res) => this.onAuthSuccess(res)),
        catchError((err) => throwError(() => this.toAuthError(err))),
      );
  }

  /**
   * Explicit refresh call. Bypasses the auth interceptor's own 401→refresh
   * flow (SKIP_AUTH_REFRESH) to avoid a refresh call ever trying to refresh
   * itself.
   */
  refresh(): Observable<AuthResponse | null> {
    const refreshToken = this.tokenStorage.getRefreshToken();

    if (!refreshToken) {
      return of(null);
    }

    return this.http
      .post<ApiResponse<AuthResponse>>(
        `${this.baseUrl}/refresh`,
        { refreshToken },
        { context: new HttpContext().set(SKIP_AUTH_REFRESH, true) },
      )
      .pipe(
        map((res) => this.onAuthSuccess(res)),
        catchError(() => {
          this.clearSession();
          return of(null);
        }),
      );
  }

  logout(): Observable<void> {
    const refreshToken = this.tokenStorage.getRefreshToken();

    if (!refreshToken) {
      this.clearSession();
      return of(void 0);
    }

    return this.http
      .post<ApiResponse<void>>(
        `${this.baseUrl}/logout`,
        { refreshToken },
        { context: new HttpContext().set(SKIP_AUTH_REFRESH, true) },
      )
      .pipe(
        map(() => void 0),
        catchError(() => of(void 0)), // logout is best-effort; always clear locally
        tap(() => this.clearSession()),
      );
  }

  me(): Observable<CurrentUser> {
    return this.http
      .get<ApiResponse<CurrentUser>>(`${this.baseUrl}/me`)
      .pipe(map((res) => res.data as CurrentUser));
  }

  getAccessToken(): string | null {
    return this.tokenStorage.getAccessToken();
  }

  getRefreshToken(): string | null {
    return this.tokenStorage.getRefreshToken();
  }

  isAccessTokenExpired(): boolean {
    return this.tokenStorage.isAccessTokenExpired();
  }

  /** Called by the interceptor once a background refresh succeeds. */
  applyRefreshedSession(auth: AuthResponse): void {
    this.onAuthSuccess({ success: true, data: auth });
  }

  clearSession(): void {
    this.tokenStorage.clear();
    this._currentUser.set(null);
  }

  private onAuthSuccess(res: ApiResponse<AuthResponse>): AuthResponse {
    const auth = res.data as AuthResponse;
    this.tokenStorage.saveSession(auth);
    this._currentUser.set({
      userId: auth.userId,
      email: auth.email,
      displayName: auth.displayName,
    });
    return auth;
  }

  private toAuthError(err: any): AuthError {
    const body = err?.error as ApiResponse<unknown> | undefined;

    return {
      message: body?.message ?? 'Something went wrong. Please try again.',
      errorCode: body?.errorCode,
      fieldErrors: body?.errors,
    };
  }
}
