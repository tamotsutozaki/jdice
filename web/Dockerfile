# Contexto de build: a pasta web/ (ver docker-compose.yml).
FROM node:22-alpine AS build
WORKDIR /src

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:1.27-alpine AS runtime

# Vai para templates/: o entrypoint do nginx roda envsubst na subida e gera o
# conf.d/default.conf injetando ${API_UPSTREAM}. O mesmo build serve o compose
# e a Azure — muda só a variável.
COPY default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /src/dist/web/browser /usr/share/nginx/html

EXPOSE 80
