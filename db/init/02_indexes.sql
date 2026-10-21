-- Indexes, each named after the query it serves.
-- PostgreSQL does not index foreign-key columns automatically.

BEGIN;

-- projects: list by owner; FK lookup when a user is deleted (RESTRICT check).
CREATE INDEX ix_projects_owner_id ON projects (owner_id);
CREATE INDEX ix_projects_status   ON projects (status);

-- tasks: per-project analytics groups by status, so (project_id, status) covers
-- both the FK and the GROUP BY.
CREATE INDEX ix_tasks_project_status ON tasks (project_id, status);

-- Workload per assignee only counts open work: partial index keeps it small.
CREATE INDEX ix_tasks_assignee_open ON tasks (assignee_id)
    WHERE status IN ('todo', 'in_progress');

-- Overdue report: open tasks with a due date.
CREATE INDEX ix_tasks_due_open ON tasks (due_date)
    WHERE status IN ('todo', 'in_progress') AND due_date IS NOT NULL;

-- system_logs: append-only and time-ordered, so BRIN is tiny and fits range scans.
CREATE INDEX brin_system_logs_occurred_at ON system_logs USING brin (occurred_at);
CREATE INDEX ix_system_logs_entity        ON system_logs (entity_type, entity_id);
CREATE INDEX ix_system_logs_user_id       ON system_logs (user_id);
-- Containment search inside the payload, e.g. payload @> '{"status":"done"}'.
CREATE INDEX gin_system_logs_payload      ON system_logs USING gin (payload jsonb_path_ops);

COMMIT;
