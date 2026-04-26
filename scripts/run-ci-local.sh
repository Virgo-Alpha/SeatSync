#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MODE="working-tree"
KEEP_TEMP="false"

usage() {
  cat <<'EOF'
Usage: ./scripts/run-ci-local.sh [--mode working-tree|head] [--keep-temp]

Runs the same core steps as .github/workflows/ci.yml in a clean temp folder:
1) dotnet restore SeatSync.sln
2) ./scripts/run-tests-with-coverage.sh

Modes:
  working-tree  Copy current workspace (excluding .git). Includes uncommitted changes.
  head          Export committed HEAD only (closest to what CI sees after push).
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --mode)
      MODE="${2:-}"
      shift 2
      ;;
    --keep-temp)
      KEEP_TEMP="true"
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage
      exit 2
      ;;
  esac
done

if [[ "$MODE" != "working-tree" && "$MODE" != "head" ]]; then
  echo "--mode must be one of: working-tree, head" >&2
  exit 2
fi

if [[ "$MODE" == "head" ]]; then
  if [[ -n "$(git -C "$ROOT_DIR" status --porcelain)" ]]; then
    echo "Warning: --mode head uses committed HEAD only; uncommitted local changes are ignored." >&2
  fi
fi

TMP_DIR="$(mktemp -d /tmp/seatsync-ci-local.XXXXXX)"
if [[ "$KEEP_TEMP" != "true" ]]; then
  trap 'rm -rf "$TMP_DIR"' EXIT
fi

echo "Preparing CI workspace in: $TMP_DIR"
if [[ "$MODE" == "working-tree" ]]; then
  tar -C "$ROOT_DIR" --exclude=".git" -cf - . | tar -C "$TMP_DIR" -xf -
else
  git -C "$ROOT_DIR" archive --format=tar HEAD | tar -C "$TMP_DIR" -xf -
fi

echo "Running local CI simulation (mode=$MODE)..."
export CI=true
export GITHUB_ACTIONS=true
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-/tmp/seatsync-dotnet}"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
mkdir -p "$DOTNET_CLI_HOME"

pushd "$TMP_DIR" >/dev/null
dotnet restore SeatSync.sln
./scripts/run-tests-with-coverage.sh
popd >/dev/null

echo "Local CI simulation succeeded."
if [[ "$KEEP_TEMP" == "true" ]]; then
  echo "Temp workspace kept at: $TMP_DIR"
fi
