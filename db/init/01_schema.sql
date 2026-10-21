-- Schema for the project-monitoring API.
-- Owned here, not by EF migrations (ADR-0003). Runs once, on an empty data volume.

BEGIN;

-- ---------------------------------------------------------------------------
-- Enumerated types: the database rejects a status the application does not know.
-- ---------------------------------------------------------------------------
CREATE TYPE user_role       AS ENUM ('admin', 'manager', 'member');
CREATE TYPE project_status  AS ENUM ('planning', 'active', 'on_hold', 'completed', 'archived');
CREATE TYPE task_status     AS ENUM ('todo', 'in_progress', 'done', 'cancelled');
CREATE TYPE task_priority   AS ENUM ('low', 'medium', 'high', 'critical');
CREATE TYPE log_level       AS ENUM ('info', 'warning', 'error');

-- ---------------------------------------------------------------------------
-- updated_at maintained by the database, so no caller can forget it.
-- ---------------------------------------------------------------------------
CREATE FUNCTION set_updated_at() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    NEW.updated_at := now();
    RETURN NEW;
END;
$$;

-- ---------------------------------------------------------------------------
-- users
-- ---------------------------------------------------------------------------
CREATE TABLE users (
    id          bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    email       text        NOT NULL CHECK (email ~* '^[^@\s]+@[^@\s]+\.[^@\s]+$'),
    full_name   text        NOT NULL CHECK (length(btrim(full_name)) > 0),
    role        user_role   NOT NULL DEFAULT 'member',
    is_active   boolean     NOT NULL DEFAULT true,
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now()
);

-- Case-insensitive uniqueness without the citext extension.
CREATE UNIQUE INDEX ux_users_email_lower ON users (lower(email));

CREATE TRIGGER trg_users_updated_at
    BEFORE UPDATE ON users
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- ---------------------------------------------------------------------------
-- projects
-- ---------------------------------------------------------------------------
CREATE TABLE projects (
    id          bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    code        text           NOT NULL UNIQUE CHECK (code ~ '^[A-Z][A-Z0-9-]{1,19}$'),
    name        text           NOT NULL CHECK (length(btrim(name)) > 0),
    description text,
    owner_id    bigint         NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    status      project_status NOT NULL DEFAULT 'planning',
    start_date  date           NOT NULL DEFAULT current_date,
    due_date    date,
    created_at  timestamptz    NOT NULL DEFAULT now(),
    updated_at  timestamptz    NOT NULL DEFAULT now(),
    CONSTRAINT ck_projects_dates CHECK (due_date IS NULL OR due_date >= start_date)
);

CREATE TRIGGER trg_projects_updated_at
    BEFORE UPDATE ON projects
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- ---------------------------------------------------------------------------
-- tasks
-- ---------------------------------------------------------------------------
CREATE TABLE tasks (
    id              bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    project_id      bigint        NOT NULL REFERENCES projects (id) ON DELETE CASCADE,
    assignee_id     bigint        REFERENCES users (id) ON DELETE SET NULL,
    title           text          NOT NULL CHECK (length(btrim(title)) > 0),
    description     text,
    status          task_status   NOT NULL DEFAULT 'todo',
    priority        task_priority NOT NULL DEFAULT 'medium',
    estimate_hours  numeric(6,2)  CHECK (estimate_hours IS NULL OR estimate_hours >= 0),
    due_date        date,
    completed_at    timestamptz,
    created_at      timestamptz   NOT NULL DEFAULT now(),
    updated_at      timestamptz   NOT NULL DEFAULT now(),
    -- A task is done exactly when it has a completion time.
    CONSTRAINT ck_tasks_completed_at CHECK ((status = 'done') = (completed_at IS NOT NULL))
);

CREATE TRIGGER trg_tasks_updated_at
    BEFORE UPDATE ON tasks
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- ---------------------------------------------------------------------------
-- system_logs: audit trail written by the API (open-questions Q4).
-- No FK to the audited entity: the log must survive the entity's deletion.
-- ---------------------------------------------------------------------------
CREATE TABLE system_logs (
    id           bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    occurred_at  timestamptz NOT NULL DEFAULT now(),
    level        log_level   NOT NULL DEFAULT 'info',
    action       text        NOT NULL,          -- e.g. task.created, project.deleted, request.failed
    entity_type  text,                          -- 'project' | 'task' | 'user'
    entity_id    bigint,
    user_id      bigint      REFERENCES users (id) ON DELETE SET NULL,
    message      text,
    payload      jsonb       NOT NULL DEFAULT '{}'::jsonb,
    CONSTRAINT ck_system_logs_entity CHECK ((entity_type IS NULL) = (entity_id IS NULL))
);

COMMIT;
