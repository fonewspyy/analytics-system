# ADR-0006: Audit rows are written in the same statement as the change

* Status: accepted
* Date: 2026-10-21
* Requirements: R01, R11 · Open question: Q4

## Context

`system_logs` is the audit trail of the API (Q4). If the change and its log are
two separate commands, a crash or a failed second command leaves a change with
no log, or a log for a change that never happened.

## Decision

* Each create/update/delete is **one SQL statement** using data-modifying CTEs:
  `WITH upd AS (UPDATE … RETURNING *), audit AS (INSERT INTO system_logs … SELECT … FROM upd) SELECT … FROM upd`.
  Both rows commit or neither does, without an explicit transaction.
* The payload is JSONB: the new row (`to_jsonb(ins)`) on create, `{before, after}`
  on update, the deleted row on delete.
* `system_logs` has no foreign key to the audited entity, so the log outlives a deleted row.
* Unexpected errors (HTTP 500) are written as `request.failed` by the exception
  handler, best-effort: if the database itself is down, the error goes to stdout only.

## Consequences

* `user_id` on audit rows is the assignee for tasks and empty otherwise: there is
  no authentication (open-questions Q2), so there is no acting user to record.
* The audit trail can be queried with `GET /api/logs` and searched inside the
  payload through the GIN index (`payload @> '{"status":"done"}'`).
