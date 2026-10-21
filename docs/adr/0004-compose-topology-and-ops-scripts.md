# ADR-0004: Compose topology, volumes and ops scripts

* Status: accepted (backup location superseded by ADR-0007)
* Date: 2026-10-21
* Requirements: R05–R07, R13–R16

## Context

Container quality and Linux skill are half the grade. The setup must come up in
one command on a clean Linux host.

## Decision

* Services: `db` (`postgres:16-alpine`) and `api` (multi-stage build, runs as non-root).
* Networks: `backend` (`internal: true`, db + api) and `frontend` (api only). Only `api`
  publishes a port; `db` is not exposed to the host and has no route out.
* Every variable has a default in compose (`${VAR:-default}`), so `docker compose up`
  works on a clean clone without creating `.env` first; `.env` overrides them.
* Volumes: named volume `pgdata` for data; bind mount `./backups` for dumps.
* `db` healthcheck = `pg_isready`; `api` `depends_on: db: condition: service_healthy`;
  `api` exposes `/health` used by its own healthcheck.
* Config through `.env` (from `.env.example`); no credentials in compose or code.
* Log rotation: Docker `json-file` driver with `max-size`/`max-file` per service,
  plus `scripts/log-rotate.sh` for files under `./backups` and any host log dir.
* Scripts: `backup.sh` (`docker compose exec -T db pg_dump` → gzip → keep N),
  `healthcheck.sh` (container state + HTTP `/health`, non-zero exit on failure, cron-friendly),
  `monitor.sh` (`docker stats --no-stream`, threshold warnings).

## Consequences

* Scripts depend on `docker compose` v2 being on PATH.
* Cron examples go in the README rather than being installed automatically.
