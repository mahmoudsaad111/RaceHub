# syntax=docker/dockerfile:1

#################################
# Build stage
#################################
FROM node:20-alpine AS build
WORKDIR /app

# Copy package manifests first so `npm ci` is cached as its own layer.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build -- --configuration production

#################################
# Runtime stage — nginx serving the compiled SPA
#################################
FROM nginx:1.27-alpine AS runtime

COPY nginx.conf /etc/nginx/conf.d/default.conf

# Angular 18 "application" builder outputs under dist/<project>/browser.
COPY --from=build /app/dist/racehub-client/browser /usr/share/nginx/html

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s \
    CMD wget -qO- http://localhost/ || exit 1

CMD ["nginx", "-g", "daemon off;"]
