import { ApplicationConfig } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter, withHashLocation } from '@angular/router';

import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';

/**
 * Root application configuration (standalone bootstrap, Angular 18 style).
 * Providers used across the whole app are registered here instead of in
 * an NgModule: the router, and HttpClient wired with the auth interceptor
 * so every outgoing request automatically carries the bearer token and
 * transparently refreshes it on expiry.
 */
export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes, withHashLocation()),
    provideHttpClient(withInterceptors([authInterceptor])),
  ],
};
