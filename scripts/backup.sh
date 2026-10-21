#!/usr/bin/env bash
# Back up PostgreSQL with pg_dump (custom format, compressed), verify the dump is
# readable, write a SHA-256 next to it, and keep only the newest $BACKUP_KEEP.
#
#   scripts/backup.sh
#   cron:  0 2 * * *  /opt/analytics-system/scripts/backup.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
require docker sha256sum

BACKUP_DIR="${BACKUP_DIR:-$ROOT_DIR/backups}"
KEEP="${BACKUP_KEEP:-7}"

umask 077 # dumps contain every row: owner-only permissions
mkdir -p "$BACKUP_DIR"
require_running db

name="${POSTGRES_DB}_$(date +%Y%m%d-%H%M%S).dump"
target="$BACKUP_DIR/$name"
partial="$target.partial"
trap 'rm -f "$partial"' EXIT # never leave a half-written file that looks like a backup

log INFO "pg_dump $POSTGRES_DB -> $target"
compose exec -T db pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --format=custom --compress=6 > "$partial"

# A dump that pg_restore cannot list is not a backup.
compose exec -T db pg_restore --list < "$partial" > /dev/null \
    || die "verification failed: pg_restore cannot read the dump"

mv "$partial" "$target"
(cd "$BACKUP_DIR" && sha256sum "$name" > "$name.sha256")
log INFO "backup ok: $name ($(du -h "$target" | cut -f1))"

# Retention: the timestamp in the name sorts chronologically, so newest first by
# name (not mtime, which a copy or restore of the folder would reset); keep $KEEP.
mapfile -t expired < <(find "$BACKUP_DIR" -maxdepth 1 -name "${POSTGRES_DB}_*.dump" | sort -r | tail -n +"$((KEEP + 1))")
for old in "${expired[@]}"; do
    rm -f "$old" "$old.sha256"
    log INFO "retention: removed $(basename "$old")"
done
