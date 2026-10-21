# Containerized Business Analytics System

Backend API for **project monitoring** — projects, tasks, users and an audit log —
with analytics reports and CSV export.
.NET 10 (Clean Architecture) · PostgreSQL 16 · Docker Compose · Bash operations scripts.

> Note on the brief: its stack section asks for .NET, while two other lines mention Node.js.
> This implementation follows the stack section — see [ADR-0002](docs/adr/0002-dotnet-clean-architecture.md).

## Quick start

```bash
docker compose up -d --build
curl http://localhost:8080/health          # Healthy
bash scripts/smoke-test.sh                 # 33 end-to-end checks
```

That is the whole setup: no `.env` is needed, because compose has defaults for every value.
To change credentials or the port, `cp .env.example .env` and edit it before starting.

The schema and demo data (20 users, 8 projects, 400 tasks) load from `db/init/` on the
first start of an empty volume. After editing those files, `docker compose down -v` re-initialises.

Requirements: Docker with Compose v2. For the scripts: bash, curl, coreutils (any Linux).

## Architecture

```
            ┌───────────── frontend network ─────────────┐
 client ──► │ api :8080   (ASP.NET Core, non-root user)  │
            └──────┬─────────────────────────────────────┘
                   │ backend network (internal: no route out, no published port)
            ┌──────▼──────────────┐
            │ db  (postgres:16)   │──► volume pgdata
            └─────────────────────┘

src/
  Analytics.Domain          entities + business rules, no dependencies
  Analytics.Application     use cases (services), DTOs, repository interfaces
  Analytics.Infrastructure  Dapper repositories, analytics SQL, CSV writer
  Analytics.Api             controllers, error handling, CSV formatter, health check
```

Dependencies point inwards only: Api → Infrastructure → Application → Domain.
Business rules live in the domain entities (`TaskItem.ChangeStatus`, `Project.ChangeStatus`)
and services; the schema enforces the same invariants again with constraints.

## API

Base URL `http://localhost:8080`. JSON uses camelCase; enum values are snake_case,
the same labels as the database (`in_progress`, `on_hold`).

| Method | Path | Notes |
| --- | --- | --- |
| GET | `/health` | `Healthy` (200) only if PostgreSQL answers; 503 otherwise |
| GET | `/api/users` | `?role=&isActive=&limit=&offset=` |
| GET · PUT · DELETE | `/api/users/{id}` | DELETE → 409 if the user owns projects |
| POST | `/api/users` | `{ email, fullName, role }` |
| GET | `/api/projects` | `?status=&ownerId=&limit=&offset=` |
| GET · PUT · DELETE | `/api/projects/{id}` | DELETE only for planning projects or projects with no tasks; otherwise archive |
| POST | `/api/projects` | `{ code, name, description, ownerId, startDate, dueDate }` |
| GET | `/api/projects/{id}/tasks` | `?status=` |
| GET | `/api/tasks` | `?projectId=&assigneeId=&status=&priority=&overdue=&limit=&offset=` |
| GET · PUT · DELETE | `/api/tasks/{id}` | |
| POST | `/api/tasks` | `{ projectId, assigneeId, title, description, status, priority, estimateHours, dueDate }` |
| PATCH | `/api/tasks/{id}/status` | `{ status }` |
| GET | `/api/analytics/projects` | Per-project progress report, `?status=` |
| GET | `/api/analytics/workload` | Per-user open work, ranked |
| GET | `/api/analytics/trend` | Created vs completed per day, `?days=1..365` (default 30) |
| GET | `/api/logs` | Audit trail, `?level=&action=&entityType=&entityId=&from=&to=` |

Lists return `{ items, total, limit, offset }`; `limit` defaults to 50, maximum 1000.
A full request set with saved ids is in [`postman/`](postman/analytics-system.postman_collection.json)
(import into Postman, or `newman run postman/analytics-system.postman_collection.json`).

### Business rules

| Rule | Response |
| --- | --- |
| Email unique, case-insensitive | 409 |
| Project code unique; stored upper-case | 409 |
| Project `dueDate` ≥ `startDate` | 400 (`dueDate`) |
| Project owner / task assignee must exist and be active | 400 (`ownerId` / `assigneeId`) |
| A project cannot be completed while it has open tasks | 409 |
| A completed or archived project accepts no new tasks | 409 |
| A task is `done` exactly when it has `completedAt` (set and cleared automatically) | — |
| A cancelled task cannot be reopened | 409 |

### Analytics

`GET /api/analytics/projects` — one row per project, one SQL query:
`JOIN` users for the owner, `LEFT JOIN` tasks, `count(*) FILTER (WHERE …)` per status,
progress % (`done / (all − cancelled)`), overdue count, average completion time, remaining estimate.

```json
{ "code": "PRJ-001", "status": "active", "ownerName": "User 2", "totalTasks": 50,
  "todo": 6, "inProgress": 10, "done": 30, "cancelled": 4, "overdue": 11,
  "progressPercent": 65.2, "avgCompletionHours": 218.4, "remainingEstimateHours": 160.88 }
```

`GET /api/analytics/workload` — per active user: open tasks, open high/critical, overdue,
done in the last 30 days, open estimate hours, and `loadRank` (`dense_rank()` window function).

`GET /api/analytics/trend?days=30` — `generate_series` calendar so quiet days show 0,
created vs completed per day, and the running change in open work (`sum() OVER`).

### CSV export

Every list and report endpoint returns CSV with either

```bash
curl -OJ 'http://localhost:8080/api/analytics/projects?format=csv'
curl -H 'Accept: text/csv' 'http://localhost:8080/api/tasks?overdue=true&limit=1000'
```

RFC 4180 quoting, snake_case headers, ISO dates, UTF-8 with BOM (Thai text opens correctly in Excel),
`Content-Disposition` with a file name, and text cells starting with `= + - @` are prefixed with `'`
so a spreadsheet does not execute them (CSV injection).

### Errors

All errors are RFC 7807 `application/problem+json`, produced in one place
(`Api/Errors/GlobalExceptionHandler.cs`):

```json
{ "title": "Business rule violated", "status": 409,
  "detail": "Project SMK-1 still has 1 open task(s); finish or cancel them before completing it.",
  "instance": "/api/projects/9", "traceId": "00-…" }
```

| Status | When |
| --- | --- |
| 400 | Body/query validation, unknown enum value, a database `CHECK` violation |
| 404 | Unknown id or route |
| 409 | Business rule, duplicate value, referenced data |
| 500 | Anything unexpected: a generic message, no internals. Logged to stdout and to `system_logs` (`request.failed`) with the `traceId` |

## Database

| Table | Notes |
| --- | --- |
| `users` | Unique `lower(email)`, role enum |
| `projects` | Owner FK (`RESTRICT`), status enum, `CHECK (due_date >= start_date)`, code format `CHECK` |
| `tasks` | Project FK (`CASCADE`), assignee FK (`SET NULL`), `CHECK ((status = 'done') = (completed_at IS NOT NULL))` |
| `system_logs` | Audit trail, `JSONB` payload, no FK to the audited row so the log outlives it |

PostgreSQL features: enum types, `CHECK` constraints, identity columns, an `updated_at` trigger,
partial indexes on open tasks, a BRIN index on log time, a GIN index on the JSONB payload,
expression unique index, and data-modifying CTEs that write a change and its audit row in one
statement ([ADR-0006](docs/adr/0006-audit-log-in-the-same-statement.md)).

Index use, checked with `EXPLAIN` on 200,000 tasks and 200,000 log rows (inserted in a transaction and rolled back):

| Query | Index used | Size |
| --- | --- | --- |
| Tasks of a project in a status | `ix_tasks_project_status` (project_id, status) | 1.4 MB |
| Open work of an assignee | `ix_tasks_assignee_open` — partial, open tasks only | 280 kB |
| Overdue open tasks | `ix_tasks_due_open` — partial | |
| Logs in a time range | `brin_system_logs_occurred_at` — BRIN | **24 kB** (the B-tree `ix_system_logs_entity` on the same table: 6 MB) |
| `payload @> '{"status":"cancelled"}'` | `gin_system_logs_payload` (`jsonb_path_ops`) | |

## Containers

| Service | Image | Networks | Port | Health |
| --- | --- | --- | --- | --- |
| `db` | `postgres:16-alpine` | `backend` (internal) | not published | `pg_isready` |
| `api` | multi-stage: `sdk:10.0-alpine` → `aspnet:10.0-alpine`, runs as `app` (uid 1654) | `backend`, `frontend` | 8080 | `GET /health` |

* `api` starts only after `db` is healthy (`depends_on: condition: service_healthy`).
* Two named volumes: `pgdata` for the database, `pgbackups` (mounted at `/backups` in `db`) for compressed dumps.
  `docker compose down -v` removes both — export backups first (`backup.sh --export DIR`).
* The Dockerfile restores from `.csproj` files first, so the package layer is cached across code changes.
* Container logs rotate at 3 × 10 MB per service (`json-file` driver).

## Operations scripts

| Script | What it does | Exit code |
| --- | --- | --- |
| `scripts/backup.sh [--export DIR \| --list]` | `pg_dump` in custom format, **compressed** (zlib level 6), into the **`pgbackups` volume**; verified with `pg_restore --list`, SHA-256 alongside, owner-only permissions, keeps the newest `BACKUP_KEEP` (7). Named by the container clock in UTC so the order holds whichever host runs it ([ADR-0007](docs/adr/0007-backups-in-a-named-volume.md)). `--export` copies the backup to a host folder | 1 on failure |
| `scripts/restore.sh <name \| file> --yes` | Takes a backup name from the volume or a dump file from the host; checks the SHA-256, stops `api`, restores in one transaction, starts `api` and waits until it is healthy | 1 on failure |
| `scripts/healthcheck.sh [--heal]` | Each service: state, Docker health, restart count; then `/health`. `--heal` restarts unhealthy services | 1 if anything is unhealthy |
| `scripts/monitor.sh [--watch N]` | CPU / memory / network / PIDs per container, database size, backups volume size, host disk; thresholds `CPU_WARN`, `MEM_WARN`, `DISK_WARN` | 1 on a breach |
| `scripts/log-rotate.sh` | Rotates `logs/*.log` over `LOG_MAX_KB` (1024) to `.1.gz … .N.gz`, keeps `LOG_KEEP` (5); reports Docker's log limits | |
| `scripts/smoke-test.sh` | 33 end-to-end checks: CRUD, every business rule, errors, analytics, CSV, audit | 1 if any fails |

Every script writes to `logs/<script>.log`, reads `.env` if present, and passes
[shellcheck](https://www.shellcheck.net/) with no findings. Schedule them with
[`scripts/crontab.example`](scripts/crontab.example).

## Tests

```bash
dotnet test                     # 46 unit tests: domain rules, services, enum mapping, CSV
bash scripts/smoke-test.sh      # against the running stack
```

## Known limits

* No authentication: `users` are data (owners and assignees), not logins. Audit rows therefore
  record no acting user except the task assignee ([ADR-0006](docs/adr/0006-audit-log-in-the-same-statement.md)).
* Schema changes are applied by re-creating the volume, not by migrations ([ADR-0003](docs/adr/0003-schema-owned-by-sql-init.md)).
* CSV export of a list exports the requested page (`limit` up to 1000).

## Design decisions

[`docs/adr/`](docs/adr/) — why .NET with four projects, why the schema lives in SQL files,
why Dapper instead of EF Core, why audit rows share the statement with the change.
Requirement-by-requirement status: [`docs/context/requirements-trace.md`](docs/context/requirements-trace.md).
Progress notes: [`WORKING_LOG.md`](WORKING_LOG.md).
