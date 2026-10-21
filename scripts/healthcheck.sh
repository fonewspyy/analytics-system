#!/usr/bin/env bash
# Check every compose service (running? Docker health? restart loops?) and the API's
# /health endpoint. Exit 0 when all is well, 1 otherwise, so cron or a monitor can alert.
#
#   scripts/healthcheck.sh            report only
#   scripts/healthcheck.sh --heal     also restart any service that is not healthy
#   cron:  */5 * * * *  /opt/analytics-system/scripts/healthcheck.sh --heal
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
require docker curl

heal=false
[[ "${1:-}" == "--heal" ]] && heal=true
RESTART_WARN="${RESTART_WARN:-3}"
status=0

for svc in $(compose config --services); do
    cid="$(compose ps -aq "$svc")"
    if [[ -z "$cid" ]]; then
        log ERROR "$svc: container does not exist"
        status=1
        continue
    fi

    read -r state health restarts < <(docker inspect -f \
        '{{.State.Status}} {{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}} {{.RestartCount}}' "$cid")

    if [[ "$state" == "running" && ("$health" == "healthy" || "$health" == "none") ]]; then
        log INFO "$svc: state=$state health=$health restarts=$restarts"
    else
        log ERROR "$svc: state=$state health=$health restarts=$restarts"
        status=1
        if $heal; then
            log WARN "$svc: restarting"
            compose restart "$svc" > /dev/null
        fi
    fi

    if ((restarts > RESTART_WARN)); then
        log WARN "$svc has restarted $restarts times; see: docker compose logs --tail 100 $svc"
    fi
done

code="$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "$API_URL/health" || true)"
if [[ "$code" == "200" ]]; then
    log INFO "api /health: 200"
else
    log ERROR "api /health: ${code:-no response}"
    status=1
fi

exit "$status"
