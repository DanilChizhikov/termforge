#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WEB_DIR="$ROOT_DIR/src/Web"
ENTRYPOINT_PROJECT="$ROOT_DIR/src/Api/Entrypoint/Entrypoint.csproj"
BUILD_PROPS="$ROOT_DIR/Directory.Build.props"
ALL_RUNTIME_IDENTIFIERS=(linux-x64 linux-arm64 win-x64 win-arm64 osx-x64 osx-arm64)

usage() {
  cat <<EOF
Usage: $(basename "$0") [-r <rid>]... [-v <version>] [-b <build>] [-o <dir>] [-s] [-n] [-h]

Builds the frontend once, then publishes a self-contained TermForge for every
runtime identifier into <dir>/termforge-<version>-<rid> and archives it
(.zip for win-*, .tar.gz otherwise).

Options:
  -r  runtime identifier, repeatable (default: ${ALL_RUNTIME_IDENTIFIERS[*]})
  -v  version (default: <Version> from Directory.Build.props)
  -b  build number (default: 0)
  -o  output directory (default: publish/ in the repository root)
  -s  skip the frontend build (use the existing src/Api/Entrypoint/wwwroot)
  -n  do not create archives
  -h  show this help
EOF
}

runtime_identifiers=()
version=""
build_number=0
output_dir="$ROOT_DIR/publish"
skip_frontend_build=0
create_archives=1

while getopts "r:v:b:o:snh" option; do
  case "$option" in
    r) runtime_identifiers+=("$OPTARG") ;;
    v) version="$OPTARG" ;;
    b) build_number="$OPTARG" ;;
    o) output_dir="$OPTARG" ;;
    s) skip_frontend_build=1 ;;
    n) create_archives=0 ;;
    h) usage; exit 0 ;;
    *) usage >&2; exit 2 ;;
  esac
done

if [[ ${#runtime_identifiers[@]} -eq 0 ]]; then
  runtime_identifiers=("${ALL_RUNTIME_IDENTIFIERS[@]}")
fi
if [[ -z "$version" ]]; then
  version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$BUILD_PROPS")"
fi

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "error: '$1' is not installed or not in PATH" >&2
    exit 1
  fi
}

build_frontend() {
  echo "==> Building frontend into src/Api/Entrypoint/wwwroot"
  (cd "$WEB_DIR" && npm ci && npm run build)
}

publish_runtime() {
  local runtime_identifier="$1"
  local target_dir="$output_dir/termforge-$version-$runtime_identifier"
  echo "==> Publishing $runtime_identifier into $target_dir"
  rm -rf "$target_dir"
  dotnet publish "$ENTRYPOINT_PROJECT" -c Release -r "$runtime_identifier" --self-contained true --nologo \
    -o "$target_dir" -p:Version="$version" -p:BuildNumber="$build_number"
  if [[ ! -f "$target_dir/wwwroot/index.html" ]]; then
    echo "error: $target_dir/wwwroot/index.html is missing, the frontend is not included" >&2
    exit 1
  fi
}

archive_runtime() {
  local runtime_identifier="$1"
  local name="termforge-$version-$runtime_identifier"
  echo "==> Archiving $name"
  if [[ "$runtime_identifier" == win-* ]]; then
    rm -f "$output_dir/$name.zip"
    if command -v zip >/dev/null 2>&1; then
      (cd "$output_dir" && zip -qr "$name.zip" "$name")
    else
      (cd "$output_dir" && 7z a "$name.zip" "$name" >/dev/null)
    fi
  else
    (cd "$output_dir" && COPYFILE_DISABLE=1 tar --no-xattrs -czf "$name.tar.gz" "$name")
  fi
}

require_command dotnet
mkdir -p "$output_dir"
output_dir="$(cd "$output_dir" && pwd)"

if [[ $skip_frontend_build -eq 0 ]]; then
  require_command npm
  build_frontend
fi

for runtime_identifier in "${runtime_identifiers[@]}"; do
  publish_runtime "$runtime_identifier"
  if [[ $create_archives -eq 1 ]]; then
    archive_runtime "$runtime_identifier"
  fi
done

echo "==> Done: $output_dir"
