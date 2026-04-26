#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RESULTS_DIR="$ROOT_DIR/artifacts/test-results"
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-/tmp/seatsync-dotnet}"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

rm -rf "$RESULTS_DIR"
mkdir -p "$RESULTS_DIR"
mkdir -p "$DOTNET_CLI_HOME"

echo "Running tests with coverage..."
dotnet test "$ROOT_DIR/SeatSync.sln" \
  --configuration Release \
  --collect:"XPlat Code Coverage" \
  --settings "$ROOT_DIR/coverlet.runsettings" \
  --logger "trx;LogFileName=test-results.trx" \
  --results-directory "$RESULTS_DIR"

mapfile -t COVERAGE_FILES < <(find "$RESULTS_DIR" -type f -name "coverage.cobertura.xml" | sort)
if [ "${#COVERAGE_FILES[@]}" -eq 0 ]; then
  echo "No coverage files were produced."
  exit 1
fi

total_valid=0
total_covered=0
for coverage_file in "${COVERAGE_FILES[@]}"; do
  lines_valid="$(grep -o 'lines-valid="[0-9]\+"' "$coverage_file" | head -n1 | sed 's/[^0-9]//g')"
  lines_covered="$(grep -o 'lines-covered="[0-9]\+"' "$coverage_file" | head -n1 | sed 's/[^0-9]//g')"

  if [ -z "$lines_valid" ] || [ -z "$lines_covered" ]; then
    echo "Failed parsing coverage metrics from $coverage_file"
    exit 1
  fi

  total_valid=$((total_valid + lines_valid))
  total_covered=$((total_covered + lines_covered))
done

if [ "$total_valid" -eq 0 ]; then
  echo "Coverage data has zero valid lines."
  exit 1
fi

coverage_percent="$(awk -v covered="$total_covered" -v valid="$total_valid" 'BEGIN { printf "%.2f", (covered/valid)*100 }')"

echo "Coverage summary: $coverage_percent% lines ($total_covered/$total_valid)"
echo "Coverage reports:"
printf ' - %s\n' "${COVERAGE_FILES[@]}"
