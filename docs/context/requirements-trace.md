# Requirements trace

One row per gradeable requirement. Update **Status** and **Evidence** when a row is done.
Status: `todo` · `doing` · `done` · `blocked`.

| ID | Requirement (brief §) | Where it lives | Status | Evidence |
| --- | --- | --- | --- | --- |
| R01 | Tables `projects`, `tasks`, `users`, `system_logs` (§3 D1) | `db/init/01_schema.sql` | done | `\dt` → 4 tables; seed 20/8/400/400 |
| R02 | Foreign keys between them (§3 D1) | `db/init/01_schema.sql` | done | insert with project_id=999 → FK violation; RESTRICT / CASCADE / SET NULL per relation |
| R03 | Indexing for query paths (§3 D1) | `db/init/02_indexes.sql` | done | 9 indexes (+ unique email); `EXPLAIN` on 200k rows uses all five query indexes — README § Database |
| R04 | PostgreSQL-specific features (§5) | enum types / `CHECK` / `JSONB` / partial + BRIN + GIN / trigger / CTE | done | bad enum, done-without-completed_at rejected; audit via data-modifying CTE |
| R05 | API `Dockerfile` — multi-stage, non-root (§3 D1) | `src/Analytics.Api/Dockerfile` | done | `id` in container → uid=1654(app); HEALTHCHECK via busybox wget |
| R06 | `docker-compose.yml` API + Postgres, schema auto-init (§3 D1, §4.2) | `docker-compose.yml` | done | `down -v && up -d --build` → both healthy, smoke test 33/33 |
| R07 | Named volume + private network + healthcheck (§5) | `docker-compose.yml` | done | `pgdata`, `backend` internal, db port not published, healthchecks on both |
| R08 | CRUD endpoints (§3 D2) | `src/Analytics.Api/Controllers` | done | smoke test 33/33; newman 30 requests / 32 assertions, 0 failed |
| R09 | Analytics endpoint — JOIN + aggregates (§3 D2) | `Infrastructure/Persistence/AnalyticsQueries.cs` | done | 3 reports: JOIN + `FILTER` aggregates, `dense_rank()`, `generate_series` + `sum() OVER` |
| R10 | Export JSON + CSV (§3 D2) | `Api/Formatting/CsvOutputFormatter.cs`, `Infrastructure/Export/CsvWriter.cs` | done | `?format=csv` and `Accept: text/csv`; unit tests for quoting + CSV injection |
| R11 | Centralised error handling (§5) | `Api/Errors/GlobalExceptionHandler.cs` | done | 400/404/409/500 ProblemDetails; db stopped → generic 500, `/health` 503 |
| R12 | Clean Architecture layering (§2, §5) | `src/*` | done | one-way project references; domain has no package references |
| R13 | `pg_dump` backup script (§3 D3) | `scripts/backup.sh`, `scripts/restore.sh` | done | delete 50 tasks → restore → 401 rows back; retention; files 0600 on Linux |
| R14 | Container health check script (§3 D3) | `scripts/healthcheck.sh` | done | db stopped → exit 1 with reason; `--heal` restarts |
| R15 | CPU/memory monitoring script (§3 D3) | `scripts/monitor.sh` | done | snapshot + thresholds; `CPU_WARN=0` → WARN lines, exit 1 |
| R16 | Log rotation (§3 D3) | compose `x-logging` + `scripts/log-rotate.sh` | done | `LOG_MAX_KB=0 LOG_KEEP=2` → `.1.gz`/`.2.gz`, older dropped |
| R17 | README with run instructions (§4.4) | `README.md` | done | |
| R18 | Postman collection (§4.4) | `postman/analytics-system.postman_collection.json` | done | newman run, 0 failures |
| — | Scripts run on Linux | all `scripts/*.sh` | done | shellcheck 0 findings; all scripts run inside Alpine (`docker:cli`) incl. smoke 33/33 |
