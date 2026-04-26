#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MODE="working-tree"
KEEP_TEMP="false"
ONLY_RELEVANT_CHANGES="true"

usage() {
  cat <<'EOF'
Usage: ./scripts/run-ci-local.sh [--mode working-tree|head] [--keep-temp]
                                [--only-relevant-changes|--all-changes]

Runs the same core steps as .github/workflows/ci.yml in a clean temp folder:
1) dotnet restore SeatSync.sln
2) ./scripts/run-tests-with-coverage.sh

Modes:
  working-tree  Copy current workspace (excluding .git). Includes uncommitted changes.
  head          Export committed HEAD only (closest to what CI sees after push).

Change filtering:
  --only-relevant-changes (default)
                Skip CI run when only non-functional files changed.
                Relevant changes include source projects, tests, build/CI scripts,
                solution/proj files, and shared build configuration.
  --all-changes Always run CI regardless of changed file types.
EOF
}

is_relevant_ci_path() {
  local path="$1"
  case "$path" in
    SeatSync.Api/*|SeatSync.Domain/*|SeatSync.Infrastructure/*|SeatSync.Tests/*|SeatSync.Web/*)
      return 0
      ;;
    scripts/*|.github/workflows/ci.yml|Directory.Build.props|Directory.Build.targets|coverlet.runsettings|SeatSync.sln|*.csproj|global.json|NuGet.config)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

should_run_for_changes() {
  local -a changed_paths=()
  local path

  if [[ "$MODE" == "working-tree" ]]; then
    while IFS= read -r -d '' path; do
      changed_paths+=("$path")
    done < <(git -C "$ROOT_DIR" diff --name-only -z HEAD)

    while IFS= read -r -d '' path; do
      changed_paths+=("$path")
    done < <(git -C "$ROOT_DIR" ls-files --others --exclude-standard -z)
  else
    if git -C "$ROOT_DIR" rev-parse --verify HEAD~1 >/dev/null 2>&1; then
      while IFS= read -r -d '' path; do
        changed_paths+=("$path")
      done < <(git -C "$ROOT_DIR" diff --name-only -z HEAD~1 HEAD)
    else
      echo "No parent commit found for HEAD; running CI."
      return 0
    fi
  fi

  if [[ "${#changed_paths[@]}" -eq 0 ]]; then
    echo "No changed files detected for mode=$MODE; skipping CI simulation."
    return 1
  fi

  for path in "${changed_paths[@]}"; do
    if is_relevant_ci_path "$path"; then
      return 0
    fi
  done

  echo "Only non-functional changes detected; skipping CI simulation."
  return 1
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
    --only-relevant-changes)
      ONLY_RELEVANT_CHANGES="true"
      shift
      ;;
    --all-changes)
      ONLY_RELEVANT_CHANGES="false"
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

if [[ "$ONLY_RELEVANT_CHANGES" == "true" ]]; then
  if ! should_run_for_changes; then
    exit 0
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
