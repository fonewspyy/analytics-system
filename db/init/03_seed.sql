-- Demo data so the analytics endpoints return something on first start.
-- Deterministic: setseed() makes every fresh volume produce the same rows.

BEGIN;

SELECT setseed(0.42);

INSERT INTO users (email, full_name, role)
SELECT format('user%s@example.com', g),
       format('User %s', g),
       CASE WHEN g = 1 THEN 'admin' WHEN g <= 4 THEN 'manager' ELSE 'member' END::user_role
FROM generate_series(1, 20) AS g;

INSERT INTO projects (code, name, description, owner_id, status, start_date, due_date)
SELECT format('PRJ-%s', lpad(g::text, 3, '0')),
       format('Project %s', g),
       format('Demo project %s', g),
       1 + (g % 4),
       (ARRAY['planning','active','active','active','on_hold','completed']::project_status[])[1 + (g % 6)],
       current_date - (60 + g * 5),
       current_date + (g * 7 - 20)
FROM generate_series(1, 8) AS g;

-- 400 tasks spread across projects, statuses and assignees.
INSERT INTO tasks (project_id, assignee_id, title, status, priority, estimate_hours, due_date, completed_at, created_at)
SELECT t.project_id,
       t.assignee_id,
       format('Task %s', t.g),
       t.status,
       (ARRAY['low','medium','medium','high','critical']::task_priority[])[1 + floor(random() * 5)::int],
       round((1 + random() * 15)::numeric, 2),
       t.created::date + (3 + floor(random() * 30)::int),
       -- Completed somewhere between creation and now, never in the future.
       CASE WHEN t.status = 'done' THEN t.created + (now() - t.created) * random() END,
       t.created
FROM (
    SELECT g,
           1 + (g % 8)                                          AS project_id,
           CASE WHEN g % 13 = 0 THEN NULL ELSE 5 + (g % 16) END AS assignee_id,
           (ARRAY['todo','in_progress','done','done','done','cancelled']::task_status[])[1 + floor(random() * 6)::int] AS status,
           now() - make_interval(days => floor(random() * 60)::int) AS created
    FROM generate_series(1, 400) AS g
) AS t;

INSERT INTO system_logs (occurred_at, level, action, entity_type, entity_id, user_id, message, payload)
SELECT created_at, 'info', 'task.created', 'task', id, assignee_id, 'seed',
       jsonb_build_object('project_id', project_id, 'status', status, 'priority', priority)
FROM tasks;

COMMIT;
