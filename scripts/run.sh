#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WEB_DIR="$ROOT_DIR/src/Web"
ENTRYPOINT_DIR="$ROOT_DIR/src/Api/Entrypoint"
ENTRYPOINT_DLL="$ENTRYPOINT_DIR/bin/Debug/net10.0/TermForge.dll"
BACKEND_URL="http://127.0.0.1:5023"
DEV_SERVER_URL="http://127.0.0.1:5173"
BACKEND_START_TIMEOUT_SECONDS=60

usage() {
  cat <<EOF
Usage: $(basename "$0") [-d] [-s] [-c] [-h] [-- <backend args>]

Builds the frontend and the backend, then starts TermForge. The backend opens
the browser with a one-time launch link. Stop with Ctrl+C.

Options:
  -d  dev mode: backend in background + Vite dev server with hot reload
      (open $DEV_SERVER_URL in the browser the backend opened)
  -s  skip the frontend build (use the existing src/Api/Entrypoint/wwwroot)
  -c  force a clean frontend install (npm ci) even if node_modules is up to date
  -h  show this help

Arguments after -- are passed to the backend, e.g.:
  $(basename "$0") -- --Lifecycle:OpenBrowser=false --Persistence:DatabasePath=/tmp/tf.db
  $(basename "$0") -- --Kestrel:Endpoints:Http:Url=http://127.0.0.1:5199   (other port, not with -d)
EOF
}

dev_mode=0
skip_frontend_build=0
force_clean_install=0

while getopts "dsch" option; do
  case "$option" in
    d) dev_mode=1 ;;
    s) skip_frontend_build=1 ;;
    c) force_clean_install=1 ;;
    h) usage; exit 0 ;;
    *) usage >&2; exit 2 ;;
  esac
done
shift $((OPTIND - 1))
if [[ "${1:-}" == "--" ]]; then
  shift
fi

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "error: '$1' is not installed or not in PATH" >&2
    exit 1
  fi
}

install_frontend_dependencies() {
  local installed_lock="$WEB_DIR/node_modules/.package-lock.json"
  if [[ $force_clean_install -eq 1 || ! -f "$installed_lock" || "$WEB_DIR/package-lock.json" -nt "$installed_lock" ]]; then
    echo "==> Installing frontend dependencies"
    (cd "$WEB_DIR" && npm ci)
  fi
}

build_frontend() {
  echo "==> Building frontend into src/Api/Entrypoint/wwwroot"
  (cd "$WEB_DIR" && npm run build)
}

build_backend() {
  echo "==> Building backend"
  dotnet build "$ENTRYPOINT_DIR/Entrypoint.csproj" --nologo -v quiet
}

backend_pid=""

stop_backend() {
  if [[ -n "$backend_pid" ]] && kill -0 "$backend_pid" 2>/dev/null; then
    echo "==> Stopping backend (pid $backend_pid)"
    kill -TERM "$backend_pid" 2>/dev/null || true
    wait "$backend_pid" 2>/dev/null || true
  fi
}

wait_for_backend() {
  local elapsed=0
  until curl -s -o /dev/null "$BACKEND_URL/"; do
    if ! kill -0 "$backend_pid" 2>/dev/null; then
      echo "error: backend exited during startup" >&2
      exit 1
    fi
    if [[ $elapsed -ge $BACKEND_START_TIMEOUT_SECONDS ]]; then
      echo "error: backend did not answer on $BACKEND_URL within ${BACKEND_START_TIMEOUT_SECONDS}s" >&2
      exit 1
    fi
    sleep 1
    elapsed=$((elapsed + 1))
  done
}

require_command dotnet
require_command npm

if [[ $dev_mode -eq 1 || $skip_frontend_build -eq 0 ]]; then
  install_frontend_dependencies
fi
if [[ $dev_mode -eq 0 && $skip_frontend_build -eq 0 ]]; then
  build_frontend
fi
build_backend

cd "$ENTRYPOINT_DIR"

if [[ $dev_mode -eq 0 ]]; then
  echo "==> Starting TermForge (default $BACKEND_URL, Ctrl+C to stop)"
  exec dotnet "$ENTRYPOINT_DLL" "$@"
fi

require_command curl
trap stop_backend EXIT
trap 'exit 130' INT TERM

echo "==> Starting backend on $BACKEND_URL"
dotnet "$ENTRYPOINT_DLL" "$@" &
backend_pid=$!
wait_for_backend

echo "==> Backend is up. Open $DEV_SERVER_URL in the browser the backend opened (use 127.0.0.1, not localhost)"
cd "$WEB_DIR"
npm run dev
