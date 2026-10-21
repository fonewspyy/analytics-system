# ADR-0003: Schema is owned by SQL init scripts, not EF migrations

* Status: accepted (data-access part superseded by ADR-0005)
* Date: 2026-10-21
* Requirements: R01–R04, R06

## Context

Database Schema is 25% of the grade and specifically looks at PostgreSQL features
(enum types, `CHECK`, `JSONB`, partial indexes). The brief also asks that the
schema initialise on `docker compose up`. Two owners for one schema (SQL files and
EF migrations) drift apart.

## Decision

* `db/init/*.sql` is the **single owner** of the schema, mounted into
  `/docker-entrypoint-initdb.d/` and run by the Postgres image on first start
  of an empty volume. Files run in name order: `01_schema.sql`, `02_indexes.sql`, `03_seed.sql`.
* EF Core (Npgsql) is used for data access only, mapped to the existing tables.
  No `Migrations/` folder.
* The analytics query (R09) is hand-written SQL (Dapper or `FromSqlRaw`) so the
  JOIN/aggregate is readable in one place.

## Consequences

* Schema changes on an existing volume need `docker compose down -v` (fine for an assignment; stated in README).
* PostgreSQL-native features are written directly, not through EF abstractions.
