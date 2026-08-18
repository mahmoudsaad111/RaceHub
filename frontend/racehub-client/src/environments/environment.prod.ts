/**
 * Production build config. apiUrl is relative ('/api') because in
 * production the app is served by nginx (see frontend/Dockerfile +
 * nginx.conf), which reverse-proxies /api/* to the racehub-api container.
 * That avoids hardcoding a hostname and sidesteps CORS entirely in Docker.
 */
export const environment = {
  production: true,
  apiUrl: '/api',
  googleClientId: '814354377554-g3thibf841k40pe8bll3tt5kk8u0k3qb.apps.googleusercontent.com',
};
