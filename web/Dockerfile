# Contexto de build: a pasta web/ (ver docker-compose.yml).
FROM node:22-alpine AS build
WORKDIR /src

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:1.27-alpine AS runtime

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist/web/browser /usr/share/nginx/html

EXPOSE 80
