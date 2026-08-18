/**
 * Development defaults — used by `ng serve` (and `ng build` without the
 * production configuration). Replaced by environment.prod.ts in
 * production builds via the fileReplacements entry in angular.json.
 */
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5109/api',
  // Matches Authentication:Google:ClientId in the backend's appsettings.json —
  // this is the public client ID, safe to ship to the browser.
  googleClientId: '814354377554-g3thibf841k40pe8bll3tt5kk8u0k3qb.apps.googleusercontent.com',
};
