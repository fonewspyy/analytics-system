#!/usr/bin/env bash
# Resource snapshot: CPU / memory / network / PIDs per container, database size and
# host disk usage. Warns, and exits 1, when a threshold is crossed.
#
#   scripts/monitor.sh                 one snapshot
#   scripts/monitor.sh --watch 10      refresh every 10 s until Ctrl-C
#   CPU_WARN=50 MEM_WARN=70 DISK_WARN=80 scripts/monitor.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
require docker awk df

CPU_WARN="${CPU_WARN:-80}"
MEM_WARN="${MEM_WARN:-80}"
DISK_WARN="${DISK_WARN:-85}"

above() { awk -v v="$1" -v t="$2" 'BEGIN { exit !(v + 0 > t + 0) }'; }

snapshot() {
    local breach=0 ids db_size disk
    mapfile -t ids < <(compose ps -q)
    ((${#ids[@]} > 0)) || die "no running containers (run: docker compose up -d)"

    printf '\n== %s ==\n' "$(date '+%Y-%m-%d %H:%M:%S')"
    printf '%-28s %7s %7s %22s %24s %5s\n' CONTAINER CPU MEM MEM_USAGE NET_IO PIDS
    while IFS=$'\t' read -r name cpu memp mem net pids; do
        printf '%-28s %7s %7s %22s %24s %5s\n' "$name" "$cpu" "$memp" "$mem" "$net" "$pids"
        if above "${cpu%\%}" "$CPU_WARN"; then
            log WARN "$name CPU $cpu > ${CPU_WARN}%"
            breach=1
        fi
        if above "${memp%\%}" "$MEM_WARN"; then
            log WARN "$name memory $memp > ${MEM_WARN}%"
            breach=1
        fi
    done < <(docker stats --no-stream --format \
        '{{.Name}}\t{{.CPUPerc}}\t{{.MemPerc}}\t{{.MemUsage}}\t{{.NetIO}}\t{{.PIDs}}' "${ids[@]}")

    if compose ps --status running --services | grep -qx db; then
        db_size="$(compose exec -T db psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Atc \
            "SELECT pg_size_pretty(pg_database_size(current_database()))")"
        printf '\ndatabase %-16s %s\n' "$POSTGRES_DB" "$db_size"
        printf 'backups volume          %s\n' "$(in_db 'du -sh /backups | cut -f1')"
    fi

    disk="$(df -P "$ROOT_DIR" | awk 'NR == 2 { sub("%", "", $5); print $5 }')"
    printf 'host disk               %s%% used\n' "$disk"
    if above "$disk" "$DISK_WARN"; then
        log WARN "host disk ${disk}% > ${DISK_WARN}%"
        breach=1
    fi

    log INFO "snapshot taken (threshold breach: $breach)"
    return "$breach"
}

if [[ "${1:-}" == "--watch" ]]; then
    interval="${2:-10}"
    trap 'exit 0' INT TERM
    while true; do
        snapshot || true
        sleep "$interval"
    done
else
    snapshot
fi
