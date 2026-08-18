import { HttpContextToken, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { BehaviorSubject, catchError, filter, switchMap, take, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

/**
 * Requests tagged with this context (login/register/refresh/logout itself)
 * skip the 401 -> refresh -> retry flow below, so a failed refresh call
 * can never try to refresh itself.
 */
export const SKIP_AUTH_REFRESH = new HttpContextToken<boolean>(() => false);

// Module-level (not per-injection-context) so every request sees the same
// in-flight refresh instead of firing one refresh call per request.
let isRefreshing = false;
const refreshedToken$ = new BehaviorSubject<string | null>(null);

/**
 * Attaches the access token to outgoing requests and transparently
 * refreshes + retries once on a 401, so components never have to deal with
 * token expiry manually.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const accessToken = authService.getAccessToken();
  const authorizedReq = accessToken
    ? req.clone({ setHeaders: { Authorization: `Bearer ${accessToken}` } })
    : req;

  return next(authorizedReq).pipe(
    catchError((error: unknown) => {
      const skipRefresh = req.context.get(SKIP_AUTH_REFRESH);

      if (
        !(error instanceof HttpErrorResponse) ||
        error.status !== 401 ||
        skipRefresh ||
        !authService.getRefreshToken()
      ) {
        return throwError(() => error);
      }

      return handle401(authorizedReq, next, authService, router);
    }),
  );
};

function handle401(
  req: Parameters<HttpInterceptorFn>[0],
  next: Parameters<HttpInterceptorFn>[1],
  authService: AuthService,
  router: Router,
) {
  if (!isRefreshing) {
    isRefreshing = true;
    refreshedToken$.next(null);

    return authService.refresh().pipe(
      switchMap((auth) => {
        isRefreshing = false;

        if (!auth) {
          router.navigateByUrl('/auth');
          return throwError(() => new Error('Session expired. Please log in again.'));
        }

        refreshedToken$.next(auth.accessToken);
        return next(req.clone({ setHeaders: { Authorization: `Bearer ${auth.accessToken}` } }));
      }),
      catchError((err) => {
        isRefreshing = false;
        authService.clearSession();
        router.navigateByUrl('/auth');
        return throwError(() => err);
      }),
    );
  }

  // A refresh triggered by another request is already in flight — wait for
  // it instead of firing a second /auth/refresh call.
  return refreshedToken$.pipe(
    filter((token): token is string => token !== null),
    take(1),
    switchMap((token) => next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }))),
  );
}
