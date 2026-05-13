# Admin Ops, Docker, and PR Draft

## Admin changes

- Added a default development admin user:
  - username: `admin`
  - password: `admin12345`
- Added admin routes:
  - `PUT /admin/ranked-challenges/today/target`
  - `DELETE /admin/leaderboards/ranked`
- `PUT /admin/ranked-challenges/today/target` now returns a JSON payload with the date, target id, target name, and reset flag.
- Unsafe routes with `.RequireXsrf()` now validate XSRF in middleware before authz runs.

## Docker commands

Start everything:

```bash
docker compose up --build
```

Start only the database:

```bash
docker compose up db
```

Start only the database in background:

```bash
docker compose up -d db
```

Start API + database:

```bash
docker compose up --build server db
```

Start client service:

```bash
docker compose up --build client
```

Notes:

- `docker compose up` starts `db`, `server`, and `client`.
- `docker compose up db` starts only PostgreSQL.
- Starting `client` also starts `server`, which also starts `db`, due to `depends_on`.

## PR Draft

## Summary
- add admin-only ranked maintenance endpoints for changing today’s target and resetting the ranked leaderboard
- move XSRF enforcement to route-metadata middleware so unsafe routes fail on XSRF before authorization
- add full-stack Docker Compose startup plus a root repo README for local development and operations

## Details
- seed a default development admin user (`admin` / `admin12345`) without duplicating it on repeated startups
- add `PUT /admin/ranked-challenges/today/target` and return a structured JSON response instead of only `204 No Content`
- add `DELETE /admin/leaderboards/ranked` to clear ranked stats, clue usage, discovery data, sessions, and guesses while preserving challenge history
- extend `IRankedStore`/`RankedStore` with challenge updates, date-scoped ranked session deletion, and full leaderboard data clearing using `ExecuteDeleteAsync()`
- update ranked guess creation to prefer the persisted challenge target when a daily challenge already exists in the database
- align development challenge seeding with `IRankedDatasetProvider` so persisted ranked targets match runtime gameplay selection
- add integration coverage for admin flows and XSRF-before-authz behavior
- add Docker services for `db`, `server`, and `client`, including a production client image served by nginx
- add a root `README.md` with local run, Docker, and admin endpoint instructions

## Ranked & Admin behavior
- changing today’s ranked target resets today’s sessions and guesses so players can replay the daily challenge against the new country
- resetting the ranked leaderboard clears user stats and active/past ranked play data, but keeps ranked challenge history intact
- unsafe admin routes now reject missing or invalid XSRF before they fail authorization, matching the intended cookie-auth security order

## Why
- give admins a safe server-side way to recover from a bad daily target or clear ranked progression during testing/demo flows
- make XSRF handling consistent across all unsafe endpoints, including ones that also require role-based authorization
- provide a one-command full-stack local startup path while still keeping `db` selectable as a standalone service when needed
