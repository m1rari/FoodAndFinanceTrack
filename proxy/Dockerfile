# syntax=docker/dockerfile:1

FROM node:22-alpine AS webbuild
WORKDIR /app

COPY web/package.json web/package-lock.json ./
RUN npm ci

COPY web/ .
RUN npm run build

FROM caddy:alpine AS final

COPY Caddyfile /etc/caddy/Caddyfile
COPY --from=webbuild /app/dist /srv
