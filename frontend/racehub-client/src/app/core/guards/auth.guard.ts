import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Protects the app-shell routes (lobby, room, race, ...). Redirects to
 * /auth, preserving the attempted URL as a returnUrl so AuthComponent can
 * send the user back where they meant to go after logging in.
 */
export const authGuard: CanActivateFn = (_route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/auth'], { queryParams: { returnUrl: state.url } });
};
