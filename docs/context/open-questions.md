# Open questions

Questions the brief does not answer. Each one records the assumption we work under
until someone answers it. Close a question by moving it to **Resolved** with the answer.

## Open

| ID | Question | Working assumption | Affects |
| --- | --- | --- | --- |
| Q1 | The brief names both .NET and Node.js. Which one is graded? | .NET (it is in the Technical Stack section; Node.js looks like a template leftover). Stated in the README. | ADR-0002 |
| Q2 | Is authentication required? | No auth in scope; `users` is a data entity (owner, assignee), not a login. Listed under Known limits. | R08, ADR-0006 |

## Resolved

| ID | Question | Decision |
| --- | --- | --- |
| Q3 | What does "complex query" need to answer? | Three reports: per-project progress, per-user workload, daily trend (R09). |
| Q4 | Where does `system_logs` come from? | Written by the API in the same statement as each change, plus `request.failed` for 500s (ADR-0006). |
| Q5 | Backup destination and retention? | Named volume `pgbackups`, compressed custom-format dumps, newest 7 kept (`BACKUP_KEEP`), SHA-256 per dump; `--export` for off-host copies (ADR-0007). The brief's Thai text asks for a designated volume. |
