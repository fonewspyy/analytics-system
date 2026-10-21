#!/usr/bin/env bash
# Restore a dump made by backup.sh. Replaces the current data, so it needs --yes.
# The API is stopped during the restore so nothing writes half-way through.
#
#   scripts/restore.sh backups/analytics_20261021-020000.dump --yes
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
require docker sha256sum

dump="${1:-}"
[[ -n "$dump" && -f "$dump" ]] || die "usage: $0 <file.dump> --yes"
[[ "${2:-}" == "--yes" ]] || die "this replaces all data in '$POSTGRES_DB'; re-run with --yes to confirm"

if [[ -f "$dump.sha256" ]]; then
    (cd "$(dirname "$dump")" && sha256sum --check --quiet "$(basename "$dump").sha256") \
        || die "checksum mismatch: $dump"
    log INFO "checksum ok"
fi

require_running db
log INFO "stopping api"
compose stop api > /dev/null

restart_api() {
    log INFO "starting api"
    compose start api > /dev/null
}
trap restart_api EXIT

log INFO "restoring $dump into $POSTGRES_DB"
compose exec -T db pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" \
    --clean --if-exists --no-owner --single-transaction < "$dump"
log INFO "restore ok"
