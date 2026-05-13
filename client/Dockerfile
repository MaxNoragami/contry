FROM oven/bun:1-alpine AS build
WORKDIR /repo

RUN apk add --no-cache python3

COPY client/package.json client/bun.lock ./client/
WORKDIR /repo/client
RUN bun install --frozen-lockfile

WORKDIR /repo
COPY client/ ./client/
COPY server/datasets/ ./server/datasets/

WORKDIR /repo/client

ARG VITE_API_BASE_URL=http://localhost:8080
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}

RUN bun run build

FROM nginx:1.29-alpine AS final
WORKDIR /usr/share/nginx/html

COPY client/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /repo/client/dist ./

EXPOSE 80
