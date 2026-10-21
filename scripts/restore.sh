#!/usr/bin/env bash
# Restore a backup. Replaces the current data, so it needs --yes.
# The argument is a backup name in the pgbackups volume (see: backup.sh --list),
# or a dump file on the host, which is copied into the volume first.
# The API is stopped during the restore so nothing writes half-way through.
#
#   scripts/restore.sh analytics_20261021-020000.dump --yes
#   scripts/restore.sh ./exported/analytics_20261021-020000.dump --yes
#
# shellcheck disable=SC2016  # the single-quoted scripts run inside the container
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
require docker

arg="${1:-}"
[[ -n "$arg" ]] || die "usage: $0 <backup-name | host-file> --yes"
[[ "${2:-}" == "--yes" ]] || die "this replaces all data in '$POSTGRES_DB'; re-run with --yes to confirm"
require_running db

if [[ -f "$arg" ]]; then
    name="$(basename "$arg")"
    log INFO "copying $arg into the backup volume"
    compose cp "$arg" "db:/backups/$name" > /dev/null 2>&1
    if [[ -f "$arg.sha256" ]]; then
        compose cp "$arg.sha256" "db:/backups/$name.sha256" > /dev/null 2>&1
    fi
else
    name="$arg"
fi

in_db 'test -f "/backups/$1"' "$name" || die "no backup named '$name' (list them: scripts/backup.sh --list)"
in_db 'cd /backups && { [ ! -f "$1.sha256" ] || sha256sum -c -s "$1.sha256"; }' "$name" \
    || die "checksum mismatch: $name"
log INFO "checksum ok"

log INFO "stopping api"
compose stop api > /dev/null 2>&1

restart_api() {
    log INFO "starting api (waiting until healthy)"
    compose up -d --wait --wait-timeout 90 api > /dev/null 2>&1 || log ERROR "api did not become healthy; see: docker compose logs api"
}
trap restart_api EXIT

log INFO "restoring $name into $POSTGRES_DB"
in_db 'pg_restore -U "$2" -d "$3" --clean --if-exists --no-owner --single-transaction "/backups/$1"' \
    "$name" "$POSTGRES_USER" "$POSTGRES_DB"
log INFO "restore ok"
