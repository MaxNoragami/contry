# Contry

Monorepo for the `Contry` country-guessing game.

## Repo layout

- `client/` - Svelte 5 SPA
- `server/` - ASP.NET Core API with PostgreSQL persistence
- `workspace.local/notes/` - local planning and implementation notes

## Current architecture

- Arcade mode remains client-authoritative.
- Ranked mode is backend-authoritative and requires authentication.
- Auth uses access and refresh cookies.
- Unsafe authenticated requests require `X-XSRF-TOKEN`.

## Development defaults

- API: `http://localhost:8080`
- Client: `http://localhost:5173`
- PostgreSQL: `localhost:5432`

The server requires bootstrap admin credentials through environment configuration in every environment. On startup it creates that admin user only if it does not already exist:

- `AdminBootstrap__Username`
- `AdminBootstrap__Email`
- `AdminBootstrap__Password`

Development startup additionally seeds fake ranked/demo users and historical ranked data.

CORS origins can include wildcard HTTPS ngrok subdomains via `https://*.ngrok-free.app` in `Cors__AllowedOriginsCsv`.

When the SPA runs under Vite dev, API-style requests should stay same-origin and be proxied by Vite to the local development API (`http://localhost:5087`). This also makes `*.ngrok-free.app` dev tunnels work without trying to call the browser's own `localhost`.

## Running locally without Docker

### Server

```bash
cd server
dotnet run --project src/Contry.Api
```

### Client

```bash
cd client
bun install
bun run dev
```

## Running with Docker Compose

Start the full stack:

```bash
docker compose up --build
```

Start only PostgreSQL:

```bash
docker compose up db
```

Start PostgreSQL in the background:

```bash
docker compose up -d db
```

Start only the API and database:

```bash
docker compose up --build server db
```

Start only the client:

```bash
docker compose up --build client
```

Because `client` depends on `server`, and `server` depends on `db`, starting `client` will also bring up the full stack.

Stop the stack:

```bash
docker compose down
```

Stop the stack and remove the database volume:

```bash
docker compose down -v
```

## Useful commands

### Server

```bash
cd server
dotnet build
dotnet test
```

### Client

```bash
cd client
bun run check
bun run build
```

## Admin endpoints

- `GET /ranked/challenges/{date}` - admin only
- `PUT /ranked/challenges/{date}` - admin only
- `DELETE /ranked/challenges/{date}` - admin only
- `DELETE /leaderboards/ranked` - admin only

Example:

```json
{
  "countryId": "FR"
}
```

These endpoints require an authenticated admin session and a valid `X-XSRF-TOKEN` header.
