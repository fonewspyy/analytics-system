#!/usr/bin/env bash
# Rotate the ops scripts' own logs in logs/ so they cannot fill the disk: a file
# over $LOG_MAX_KB is compressed to .1.gz (older ones shift to .2.gz, ...) and
# truncated in place; only $LOG_KEEP generations are kept.
#
# Container stdout/stderr is rotated by Docker itself (json-file driver with
# max-size/max-file in docker-compose.yml); this script reports those limits.
#
#   scripts/log-rotate.sh
#   cron:  0 * * * *  /opt/analytics-system/scripts/log-rotate.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
require gzip docker

MAX_KB="${LOG_MAX_KB:-1024}"
KEEP="${LOG_KEEP:-5}"

rotate() {
    local file=$1 i
    rm -f "$file.$KEEP.gz"
    for ((i = KEEP - 1; i >= 1; i--)); do
        if [[ -f "$file.$i.gz" ]]; then
            mv -f "$file.$i.gz" "$file.$((i + 1)).gz"
        fi
    done
    # Copy then truncate: a process appending to the file keeps writing to the same inode.
    gzip -c "$file" > "$file.1.gz"
    : > "$file"
}

rotated=0
for file in "$LOG_DIR"/*.log; do
    [[ -f "$file" ]] || continue
    size_kb=$(($(wc -c < "$file") / 1024))
    if ((size_kb >= MAX_KB)); then
        rotate "$file"
        rotated=$((rotated + 1))
        log INFO "rotated $(basename "$file") (${size_kb} KB)"
    fi
done
log INFO "script logs: $rotated rotated (limit ${MAX_KB} KB, keep $KEEP)"

for cid in $(compose ps -q); do
    policy="$(docker inspect -f \
        '{{.Name}} driver={{.HostConfig.LogConfig.Type}} {{range $k, $v := .HostConfig.LogConfig.Config}}{{$k}}={{$v}} {{end}}' "$cid")"
    log INFO "container log policy: ${policy#/}"
done
