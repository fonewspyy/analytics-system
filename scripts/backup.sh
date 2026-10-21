#!/usr/bin/env bash
# Back up PostgreSQL into the Docker volume `pgbackups` (mounted at /backups in the
# db container): pg_dump in custom format, compressed (zlib level 6), verified with
# pg_restore --list, SHA-256 alongside, newest $BACKUP_KEEP kept.
#
#   scripts/backup.sh                 take a backup
#   scripts/backup.sh --export DIR    take a backup and also copy it to DIR on the host
#   scripts/backup.sh --list          list the backups in the volume
#   cron:  0 2 * * *  /opt/analytics-system/scripts/backup.sh
#
# shellcheck disable=SC2016  # the single-quoted scripts run inside the container
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
require docker

KEEP="${BACKUP_KEEP:-7}"
require_running db

if [[ "${1:-}" == "--list" ]]; then
    in_db 'cd /backups 2>/dev/null && ls -1 *.dump 2>/dev/null | sort -r | while read -r f; do
               printf "%6s  %s\n" "$(du -h "$f" | cut -f1)" "$f"
           done'
    exit 0
fi

export_dir=""
if [[ "${1:-}" == "--export" ]]; then
    export_dir="${2:?usage: $0 --export DIR}"
fi

log INFO "pg_dump $POSTGRES_DB -> volume pgbackups:/backups"

# Everything below runs in the container, next to the data: nothing crosses the
# Docker API as a stream, and a failure never leaves a file that looks like a backup.
result="$(in_db '
    set -eu
    umask 077                                  # dumps hold every row: owner-only
    db=$1 user=$2 keep=$3
    # Named by the container clock, in UTC: every backup gets its name from the same
    # clock whichever host or timezone ran this script, and retention relies on that order.
    name="${db}_$(date -u +%Y%m%dT%H%M%SZ).dump"
    echo "name $name"
    cd /backups
    rm -f ./*.partial                          # left over from an interrupted run
    if ! pg_dump -U "$user" -d "$db" --format=custom --compress=6 --file="$name.partial"; then
        rm -f "$name.partial"; echo "error pg_dump failed"; exit 1
    fi
    if ! pg_restore --list "$name.partial" > /dev/null; then
        rm -f "$name.partial"; echo "error pg_restore cannot read the dump"; exit 1
    fi
    mv "$name.partial" "$name"
    sha256sum "$name" > "$name.sha256"
    echo "size $(du -h "$name" | cut -f1)"
    # Retention: the timestamp in the name sorts chronologically; keep the newest $keep.
    ls -1 "${db}"_*.dump | sort -r | tail -n +$((keep + 1)) | while read -r old; do
        rm -f "$old" "$old.sha256"
        echo "removed $old"
    done
' "$POSTGRES_DB" "$POSTGRES_USER" "$KEEP")" || die "${result##*error }"

name=""

while read -r kind value; do
    case "$kind" in
        name) name="$value" ;;
        size) log INFO "backup ok: $name ($value, compressed)" ;;
        removed) log INFO "retention: removed $value" ;;
    esac
done <<< "$result"

if [[ -n "$export_dir" ]]; then
    mkdir -p "$export_dir"
    compose cp "db:/backups/$name" "$export_dir/" > /dev/null 2>&1
    compose cp "db:/backups/$name.sha256" "$export_dir/" > /dev/null 2>&1
    log INFO "exported to $export_dir/$name"
fi
