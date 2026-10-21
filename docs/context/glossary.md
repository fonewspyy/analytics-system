# Glossary

| Term | Meaning in this repo |
| --- | --- |
| Project | A unit of work with a start/due date and an owner (`users.id`). |
| Task | Belongs to exactly one project; has status, priority, assignee, due date. |
| Task status | `todo` → `in_progress` → `done`; `cancelled` is terminal. |
| Overdue | `due_date < today` and status not in (`done`, `cancelled`). |
| Progress % | `done / (all − cancelled)` per project. |
| System log | Row in `system_logs` written by the API: action, entity, entity id, payload (`JSONB`), level, timestamp. |
| One-command setup | `docker compose up -d --build` on a clean machine brings up a working API with schema and seed data. |
