# Working log

Append-only. Newest entry at the bottom. One entry per working session or finished requirement.

```text
YYYY-MM-DD HH:mm | Area | What changed / what was learned
Evidence: command output, test result, file:line
Next: the single next step
```

---

2026-10-21 20:30 | setup | Project scaffolded: ADR 0001–0004, brief transcribed, requirements trace R01–R18, open questions Q1–Q5.
Evidence: docs/context/assignment-brief.md, docs/context/requirements-trace.md
Next: Day 1 — write db/init/01_schema.sql (R01, R02), then docker-compose.yml (R06).

2026-10-21 20:35 | day1 | Schema (4 tables, 5 enums, trigger, 9 indexes, seed), .NET solution (Domain/Application/Infrastructure/Api + Tests, net10.0), `/health` checks Postgres, multi-stage non-root Dockerfile, compose with internal backend network + named volume + healthchecks + log rotation.
Evidence: `docker compose up -d --build` on empty volume → api & db healthy, `/health` 200, counts 20/8/400/400; bad enum, done-without-completed_at, missing FK, duplicate email all rejected; container runs as uid 1654(app).
Next: Day 2 — Domain entities + CRUD for projects/tasks/users (R08) with ProblemDetails middleware (R11).

2026-10-21 20:45 | day2 | Domain entities + rules, Application services, Dapper repositories with audit CTEs, 3 analytics reports, CSV export (formatter + RFC 4180 writer with CSV-injection guard), GlobalExceptionHandler (ProblemDetails), snake_case enum binder for query strings. ADR-0005 (Dapper over EF Core), ADR-0006 (audit in the same statement).
Evidence: scripts/smoke-test.sh 33/33; newman 30 requests / 32 assertions, 0 failed; db stopped → generic 500 + /health 503; EXPLAIN on 200k rows uses all five query indexes (rolled back).
Next: Day 3 ops scripts.

2026-10-21 20:51 | day3 | backup/restore/healthcheck/monitor/log-rotate/smoke-test scripts + crontab example; 46 unit tests; README rewritten as the API + ops document; requirements R01–R18 done.
Evidence: restore round-trip 351 → 401 rows; retention by name; shellcheck 0 findings; all scripts run in Alpine (docker:cli) incl. smoke 33/33, dumps 0600; fresh `down -v && up -d --build` → smoke 33/33, healthcheck exit 0; dotnet build 0 warnings, dotnet test 46/46.
Next: owner review + commit; open questions Q1 (.NET vs Node.js) and Q2 (auth) stay as stated assumptions in the README.
