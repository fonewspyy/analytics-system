#!/usr/bin/env bash
# Shared helpers for the ops scripts. Sourced, not executed.
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

# Same defaults as docker-compose.yml; .env overrides them.
if [[ -f .env ]]; then
    set -a
    # shellcheck disable=SC1091
    source ./.env
    set +a
fi
: "${POSTGRES_DB:=analytics}"
: "${POSTGRES_USER:=analytics}"
: "${API_PORT:=8080}"
: "${API_URL:=http://localhost:${API_PORT}}"

LOG_DIR="${LOG_DIR:-$ROOT_DIR/logs}"
mkdir -p "$LOG_DIR"
LOG_FILE="${LOG_FILE:-$LOG_DIR/$(basename "$0" .sh).log}"

compose() { docker compose "$@"; }

log() {
    local level=$1
    shift
    printf '%s [%-5s] %s\n' "$(date '+%Y-%m-%dT%H:%M:%S%z')" "$level" "$*" | tee -a "$LOG_FILE" >&2
}

die() {
    log ERROR "$*"
    exit 1
}

require() {
    local cmd
    for cmd in "$@"; do
        command -v "$cmd" > /dev/null 2>&1 || die "required command not found: $cmd"
    done
}

require_running() {
    compose ps --status running --services | grep -qx "$1" \
        || die "service '$1' is not running (run: docker compose up -d)"
}
