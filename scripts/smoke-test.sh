#!/usr/bin/env bash
# End-to-end check against the running stack: CRUD, business rules, error mapping,
# analytics and CSV export. Creates its own data with a unique suffix, so it can be
# re-run. Exit 0 = every check passed.
#
#   docker compose up -d --build && scripts/smoke-test.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
require curl

BASE="$API_URL"
RUN="$(date +%s)"
passed=0
failed=0
BODY=""
CODE=""

# call METHOD PATH [JSON] -> sets CODE and BODY
call() {
    local method=$1 path=$2 data=${3:-}
    local args=(-s -w '\n%{http_code}' -X "$method" "$BASE$path") out
    [[ -n "$data" ]] && args+=(-H 'Content-Type: application/json' -d "$data")
    # Body and status in one capture: the status code is the last line.
    out="$(curl "${args[@]}")"
    CODE="${out##*$'\n'}"
    BODY="${out%$'\n'*}"
}

check() {
    local name=$1 expected=$2 needle=${3:-}
    if [[ "$CODE" == "$expected" && (-z "$needle" || "$BODY" == *"$needle"*) ]]; then
        printf '  ok    %-58s %s\n' "$name" "$CODE"
        passed=$((passed + 1))
    else
        printf '  FAIL  %-58s got %s, want %s %s\n        %s\n' "$name" "$CODE" "$expected" "${needle:+containing \"$needle\"}" "${BODY:0:300}"
        failed=$((failed + 1))
    fi
}

json_id() { grep -o '"id":[0-9]*' <<< "$BODY" | head -1 | cut -d: -f2; }

echo "smoke test against $BASE (run $RUN)"

call GET /health
check "health" 200 Healthy

# --- users -----------------------------------------------------------------
call POST /api/users "{\"email\":\"smoke$RUN@example.com\",\"fullName\":\"Smoke $RUN\",\"role\":\"manager\"}"
check "create user" 201 '"role":"manager"'
user_id="$(json_id)"

call POST /api/users "{\"email\":\"SMOKE$RUN@EXAMPLE.COM\",\"fullName\":\"Dup\"}"
check "duplicate email (case-insensitive) -> 409" 409 "already exists"

call POST /api/users '{"email":"not-an-email","fullName":"x"}'
check "invalid email -> 400" 400 '"Email"'

call POST /api/users "{\"email\":\"x$RUN@example.com\",\"fullName\":\"x\",\"role\":\"superuser\"}"
check "unknown enum value -> 400" 400

call GET "/api/users/$user_id"
check "get user" 200 "smoke$RUN@example.com"

# --- projects --------------------------------------------------------------
call POST /api/projects "{\"code\":\"smk-$RUN\",\"name\":\"Smoke project\",\"ownerId\":$user_id,\"startDate\":\"2026-01-01\",\"dueDate\":\"2026-12-31\"}"
check "create project (code upper-cased)" 201 "\"code\":\"SMK-$RUN\""
project_id="$(json_id)"

call POST /api/projects "{\"code\":\"bad-$RUN\",\"name\":\"x\",\"ownerId\":$user_id,\"startDate\":\"2026-05-01\",\"dueDate\":\"2026-04-01\"}"
check "due before start -> 400" 400 dueDate

call POST /api/projects "{\"code\":\"own-$RUN\",\"name\":\"x\",\"ownerId\":999999}"
check "unknown owner -> 400" 400 ownerId

call POST /api/projects "{\"code\":\"smk-$RUN\",\"name\":\"x\",\"ownerId\":$user_id}"
check "duplicate project code -> 409" 409 "code already exists"

call PUT "/api/projects/$project_id" "{\"name\":\"Smoke project\",\"ownerId\":$user_id,\"status\":\"active\",\"startDate\":\"2026-01-01\",\"dueDate\":\"2026-12-31\"}"
check "activate project" 200 '"status":"active"'

# --- tasks -----------------------------------------------------------------
call POST /api/tasks "{\"projectId\":$project_id,\"assigneeId\":$user_id,\"title\":\"First\",\"priority\":\"high\",\"estimateHours\":4}"
check "create task" 201 '"status":"todo"'
task1="$(json_id)"

call POST /api/tasks "{\"projectId\":$project_id,\"title\":\"Second\"}"
check "create unassigned task" 201
task2="$(json_id)"

call PATCH "/api/tasks/$task1/status" '{"status":"done"}'
check "task -> done sets completedAt" 200 '"completedAt":"'

call PUT "/api/projects/$project_id" "{\"name\":\"Smoke project\",\"ownerId\":$user_id,\"status\":\"completed\",\"startDate\":\"2026-01-01\"}"
check "complete project with open task -> 409" 409 "open task"

call PATCH "/api/tasks/$task2/status" '{"status":"cancelled"}'
check "cancel task" 200 '"status":"cancelled"'

call PATCH "/api/tasks/$task2/status" '{"status":"todo"}'
check "reopen cancelled task -> 409" 409 cancelled

call PUT "/api/projects/$project_id" "{\"name\":\"Smoke project\",\"ownerId\":$user_id,\"status\":\"completed\",\"startDate\":\"2026-01-01\"}"
check "complete project when nothing is open" 200 '"status":"completed"'

call POST /api/tasks "{\"projectId\":$project_id,\"title\":\"Late\"}"
check "add task to completed project -> 409" 409 "does not accept"

call GET "/api/projects/$project_id/tasks?status=done"
check "project tasks filtered by status" 200 '"total":1'

call GET "/api/tasks?projectId=$project_id&status=in_progress"
check "snake_case enum in query string" 200 '"total":0'

# --- analytics & export ------------------------------------------------------
call GET "/api/analytics/projects?status=completed"
check "analytics: project summary" 200 "\"progressPercent\":100"

call GET /api/analytics/workload
check "analytics: workload" 200 loadRank

call GET "/api/analytics/trend?days=7"
check "analytics: trend" 200 netOpenChange

call GET "/api/analytics/trend?days=0"
check "analytics: trend days out of range -> 400" 400 days

call GET "/api/analytics/projects?format=csv"
check "export CSV (?format=csv)" 200 "SMK-$RUN"

call GET "/api/tasks?projectId=$project_id&format=csv"
check "export page as CSV" 200 "id,project_id,assignee_id,title"

# --- audit trail -------------------------------------------------------------
call GET "/api/logs?entityType=project&entityId=$project_id"
check "audit log records project changes" 200 project.updated

# --- deletes and not-found ---------------------------------------------------
call DELETE "/api/users/$user_id"
check "delete user who owns a project -> 409" 409 owns

call DELETE "/api/projects/$project_id"
check "delete worked-on project -> 409 (archive instead)" 409 archived

call DELETE "/api/tasks/$task2"
check "delete task" 204

call GET "/api/tasks/$task2"
check "deleted task -> 404 ProblemDetails" 404 '"title":"Not found"'

call GET /api/does-not-exist
check "unknown route -> 404" 404

echo
echo "passed: $passed  failed: $failed"
log INFO "smoke test run $RUN: passed=$passed failed=$failed"
((failed == 0))
