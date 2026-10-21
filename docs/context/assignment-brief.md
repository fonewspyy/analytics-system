# Assignment brief — Containerized Business Analytics System

> Source: `Containerized Business Analytics System_C_.pdf` (received 2026-10-21).
> The PDF's Thai text does not survive text extraction; this is the structure as read,
> with Thai wording reconstructed. Check against the original PDF before quoting it.

## 1. Project goal

Build a Backend API for **Project Monitoring**.

## 2. Technical stack

| Layer | Required |
| --- | --- |
| Backend | .NET ≥ 8, Clean Architecture |
| Database | PostgreSQL |
| Infrastructure | Docker & Docker Compose |
| OS | Linux / Unix based |

## 3. Daily tasks

### Day 1 — Database architecture & containerization
* PostgreSQL design for project management: tables `projects`, `tasks`, `users`, `system_logs`, with foreign-key relations and indexing.
* Dockerization: a `Dockerfile` for the API and a `docker-compose.yml` that also runs the PostgreSQL container.
* Task: schema initialises automatically on `docker compose up`.

### Day 2 — Advanced API & business logic
* REST API: CRUD endpoints.
* Complex query: an analytics endpoint using advanced SQL (JOIN, aggregate functions).
* Data export: return results as JSON **and** CSV through the API.

### Day 3 — Linux operations & security
* Bash automation around the containers:
  * database backup with `pg_dump` (to a volume / host path)
  * container health check
* System admin:
  * resource usage (CPU / memory) per container
  * log rotation so logs do not fill the disk

## 4. Deliverables

1. GitHub repository with the source code
2. Docker setup — `docker-compose.yml` that brings everything up in **one command**
3. Bash scripts (`.sh`) for backup and monitoring
4. API documentation — `README.md` and a Postman collection

## 5. Evaluation (4 × 25%)

| Criterion | Weight | What it looks at |
| --- | --- | --- |
| Database schema | 25% | Relations, use of PostgreSQL features |
| Container quality | 25% | Dockerfile, volumes, network |
| Backend logic | 25% | Code structure, error handling |
| Linux/Unix skill | 25% | Scripting, infrastructure management |

Submission: a link to the Git repository (with a README explaining how to run it).

## Inconsistency in the brief

The stack says **.NET**, but Day 1 ("Dockerfile for the Node.js API") and the
Backend-logic criterion mention **Node.js**. Treated as a template leftover —
see `open-questions.md` Q1 and ADR-0002.
