#!/usr/bin/env bash
set -euo pipefail

DEFAULT_PORT=5099
DEFAULT_TIMEOUT_SECONDS=60

usage() {
  cat <<EOF
Usage: $(basename "$0") [-p <port>] [-t <seconds>] [-h] <publish-dir>

Starts a published TermForge from <publish-dir> with a temporary database,
takes the one-time launch link from a fake browser command, creates a browser
session, checks that the session API and the bundled frontend answer, prints
the active credential store, then requests an explicit exit through the API and expects exit code 0.

Options:
  -p  port on 127.0.0.1 (default: $DEFAULT_PORT)
  -t  timeout in seconds for startup and for shutdown (default: $DEFAULT_TIMEOUT_SECONDS)
  -h  show this help
EOF
}

port=$DEFAULT_PORT
timeout_seconds=$DEFAULT_TIMEOUT_SECONDS

while getopts "p:t:h" option; do
  case "$option" in
    p) port="$OPTARG" ;;
    t) timeout_seconds="$OPTARG" ;;
    h) usage; exit 0 ;;
    *) usage >&2; exit 2 ;;
  esac
done
shift $((OPTIND - 1))

if [[ $# -ne 1 ]]; then
  usage >&2
  exit 2
fi

publish_dir="$(cd "$1" && pwd)"
origin="http://127.0.0.1:$port"
work_dir="$(mktemp -d)"
log_file="$work_dir/termforge.log"
url_file="$work_dir/launch-url.txt"
cookie_jar="$work_dir/cookies.txt"
app_pid=""

native_path() {
  if command -v cygpath >/dev/null 2>&1; then
    cygpath -w "$1"
  else
    printf '%s' "$1"
  fi
}

fail() {
  echo "error: $1" >&2
  if [[ -f "$log_file" ]]; then
    echo "--- TermForge log ---" >&2
    cat "$log_file" >&2
  fi
  exit 1
}

cleanup() {
  if [[ -n "$app_pid" ]] && kill -0 "$app_pid" 2>/dev/null; then
    kill -TERM "$app_pid" 2>/dev/null || true
    wait "$app_pid" 2>/dev/null || true
  fi
  rm -rf "$work_dir"
}

find_binary() {
  if [[ -f "$publish_dir/TermForge.exe" ]]; then
    printf '%s' "$publish_dir/TermForge.exe"
  elif [[ -x "$publish_dir/TermForge" ]]; then
    printf '%s' "$publish_dir/TermForge"
  else
    fail "no TermForge executable in $publish_dir"
  fi
}

write_browser_command() {
  local native_url_file
  native_url_file="$(native_path "$url_file")"
  if [[ "$(find_binary)" == *.exe ]]; then
    browser_command="$work_dir/browser.cmd"
    printf '@>"%s" echo %%*\r\n' "$native_url_file" > "$browser_command"
  else
    browser_command="$work_dir/browser.sh"
    printf '#!/bin/sh\nprintf "%%s\\n" "$1" > "%s"\n' "$native_url_file" > "$browser_command"
    chmod +x "$browser_command"
  fi
}

start_app() {
  local binary
  binary="$(find_binary)"
  echo "==> Starting $binary on $origin"
  "$binary" \
    "--Kestrel:Endpoints:Http:Url=$origin" \
    "--Persistence:DatabasePath=$(native_path "$work_dir/termforge.db")" \
    "--Lifecycle:BrowserCommand=$(native_path "$browser_command")" \
    "--Updates:CheckOnStartup=false" \
    > "$log_file" 2>&1 &
  app_pid=$!
}

wait_for_launch_url() {
  local elapsed=0
  until [[ -s "$url_file" ]] && grep -q '#token=' "$url_file"; do
    if ! kill -0 "$app_pid" 2>/dev/null; then
      fail "TermForge exited during startup"
    fi
    if [[ $elapsed -ge $timeout_seconds ]]; then
      fail "TermForge did not open the launch link within ${timeout_seconds}s"
    fi
    sleep 1
    elapsed=$((elapsed + 1))
  done
}

api_request() {
  curl -sS -b "$cookie_jar" -c "$cookie_jar" -H "Origin: $origin" "$@"
}

create_browser_session() {
  local launch_url token status
  launch_url="$(tr -d '\r\n' < "$url_file")"
  token="${launch_url##*#token=}"
  status="$(api_request -o /dev/null -w '%{http_code}' -H 'Content-Type: application/json' \
    -d "{\"token\":\"$token\"}" "$origin/api/session/bootstrap")"
  [[ "$status" == "204" ]] || fail "bootstrap exchange answered $status, expected 204"
  echo "==> Browser session created"
}

read_csrf_token() {
  local session_json
  session_json="$(api_request -f "$origin/api/session")" || fail "GET /api/session failed"
  csrf_token="$(printf '%s' "$session_json" | sed -n 's|.*"csrfToken":"\([^"]*\)".*|\1|p')"
  [[ -n "$csrf_token" ]] || fail "GET /api/session returned no csrfToken: $session_json"
  echo "==> Session API answered"
}

check_frontend() {
  local index_html
  index_html="$(api_request -f "$origin/")" || fail "GET / failed"
  [[ "$index_html" == *'<div id="root"'* ]] || fail "GET / did not return the bundled frontend: $index_html"
  echo "==> Frontend served"
}

report_credential_store() {
  local status_json
  status_json="$(api_request -f "$origin/api/credentials/status")" || fail "GET /api/credentials/status failed"
  echo "==> Credential store: $status_json"
}

request_exit() {
  local status
  status="$(api_request -o /dev/null -w '%{http_code}' -H 'Content-Type: application/json' \
    -H "X-CSRF-Token: $csrf_token" -d '{"waitForTransfers":false}' "$origin/api/lifecycle/exit")"
  [[ "$status" == "202" ]] || fail "exit request answered $status, expected 202"
  echo "==> Exit requested"
}

wait_for_exit() {
  local elapsed=0 exit_code
  while kill -0 "$app_pid" 2>/dev/null; do
    if [[ $elapsed -ge $timeout_seconds ]]; then
      fail "TermForge did not exit within ${timeout_seconds}s after the exit request"
    fi
    sleep 1
    elapsed=$((elapsed + 1))
  done
  exit_code=0
  wait "$app_pid" || exit_code=$?
  app_pid=""
  [[ $exit_code -eq 0 ]] || fail "TermForge exited with code $exit_code"
  echo "==> TermForge exited with code 0"
}

trap cleanup EXIT

command -v curl >/dev/null 2>&1 || fail "'curl' is not installed or not in PATH"
browser_command=""
csrf_token=""
write_browser_command
start_app
wait_for_launch_url
create_browser_session
read_csrf_token
check_frontend
report_credential_store
request_exit
wait_for_exit
echo "==> Smoke test passed"
