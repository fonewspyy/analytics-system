# ADR-0005: Dapper and plain SQL for data access

* Status: accepted
* Date: 2026-10-21
* Requirements: R08, R09, R12 · Supersedes the data-access part of ADR-0003

## Context

ADR-0003 made `db/init/*.sql` the only owner of the schema and planned EF Core for
data access. While building Day 2 three things pushed the other way:

* The schema uses PostgreSQL enum types. EF Core needs `MapEnum` per type on the
  data source *and* the model, and the analytics query would still be raw SQL.
* Every write must also insert an audit row atomically (ADR-0006). That is one
  SQL statement with data-modifying CTEs; EF Core cannot express it, so it would
  be raw SQL inside EF anyway.
* With no migrations, EF Core would only map rows, which Dapper does with far less setup.

## Decision

* **Dapper** over a single pooled `NpgsqlDataSource`. All SQL is written by hand
  in the repositories under `Infrastructure/Persistence`, one statement per operation.
* Enums cross the boundary as their PostgreSQL label (`DbEnum`): written as
  `@status::task_status`, read as `status::text`. The JSON API and CSV use the same
  snake_case labels (`in_progress`, `on_hold`), so a value reads the same everywhere.
* Rows map to private `Row` classes and then to domain entities; domain entities
  stay free of persistence concerns.

## Options considered

| Option | For | Against |
| --- | --- | --- |
| Dapper + plain SQL | SQL is visible and reviewable; native PG features; tiny setup | Hand-written mapping; no change tracking |
| EF Core (Npgsql) | Familiar; LINQ; change tracking | Enum mapping setup; audit CTE and analytics would still be raw SQL; two styles in one repo |

## Consequences

* A column change touches the SQL and the `Row` class of that repository; the
  compiler does not catch a typo in SQL — the smoke test (`scripts/smoke-test.sh`) does.
* No ORM-generated queries to tune: what you read in the repository is what runs.
