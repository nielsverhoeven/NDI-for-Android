#!/usr/bin/env bash
#
# Surfaces test counts and line coverage on the run summary, so a failure is readable without
# downloading an artifact zip. Optionally enforces a minimum coverage percentage.
#
#   report-test-results.sh <results-dir> [min-coverage-percent]
#
# Exits non-zero only when a coverage minimum is supplied and not met. Test failures are the
# test runner's job to report — this never masks them.
#
set -euo pipefail

RESULTS_DIR="${1:-test-results}"
MIN_COVERAGE="${2:-0}"

summary() {
  if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
    echo "$1" >> "$GITHUB_STEP_SUMMARY"
  fi
  echo "$1"
}

if [[ ! -d "$RESULTS_DIR" ]]; then
  summary "No results directory at \`$RESULTS_DIR\` — nothing to report."
  exit 0
fi

# ── Test counts (TRX) ────────────────────────────────────────────────────────

TRX=$(find "$RESULTS_DIR" -name '*.trx' -type f 2>/dev/null | head -1)

if [[ -n "$TRX" ]]; then
  COUNTERS=$(grep -o '<Counters[^/]*/>' "$TRX" | head -1)
  read_counter() { echo "$COUNTERS" | grep -o "$1=\"[0-9]*\"" | grep -o '[0-9]*' | head -1; }

  TOTAL=$(read_counter total);   TOTAL=${TOTAL:-0}
  PASSED=$(read_counter passed); PASSED=${PASSED:-0}
  FAILED=$(read_counter failed); FAILED=${FAILED:-0}
  SKIPPED=$(( TOTAL - PASSED - FAILED ))

  if (( FAILED > 0 )); then
    ICON="Failed"
  elif (( PASSED == 0 )); then
    ICON="No tests executed"
  else
    ICON="Passed"
  fi

  summary "### Tests — $ICON"
  summary ""
  summary "| Total | Passed | Failed | Skipped |"
  summary "|---|---|---|---|"
  summary "| $TOTAL | $PASSED | $FAILED | $SKIPPED |"
  summary ""

  # Name the failures inline — the whole point of reporting here rather than in an artifact.
  if (( FAILED > 0 )); then
    summary "**Failing tests**"
    summary ""
    grep -o 'testName="[^"]*"' "$TRX" 2>/dev/null | sed 's/testName="//;s/"$//' | head -20 \
      | while read -r name; do summary "- \`$name\`"; done
    summary ""
  fi
else
  summary "No TRX file found under \`$RESULTS_DIR\`."
  summary ""
fi

# ── Coverage (Cobertura) ─────────────────────────────────────────────────────

COVERAGE_FILE=$(find "$RESULTS_DIR" -name 'coverage.cobertura.xml' -type f 2>/dev/null | head -1)

if [[ -z "$COVERAGE_FILE" ]]; then
  exit 0
fi

# Cobertura reports rates as 0..1 on the root <coverage> element.
LINE_RATE=$(grep -o 'line-rate="[0-9.]*"' "$COVERAGE_FILE" | head -1 | grep -o '[0-9.]*')
BRANCH_RATE=$(grep -o 'branch-rate="[0-9.]*"' "$COVERAGE_FILE" | head -1 | grep -o '[0-9.]*')

if [[ -z "$LINE_RATE" ]]; then
  summary "Coverage file found but no line-rate could be read."
  exit 0
fi

pct() { awk -v r="$1" 'BEGIN { printf "%.1f", r * 100 }'; }

LINE_PCT=$(pct "$LINE_RATE")
BRANCH_PCT=$([[ -n "$BRANCH_RATE" ]] && pct "$BRANCH_RATE" || echo "n/a")

summary "### Coverage"
summary ""
summary "| Metric | Value |"
summary "|---|---|"
summary "| Line coverage | **${LINE_PCT}%** |"
summary "| Branch coverage | ${BRANCH_PCT}% |"
summary ""

if [[ "$MIN_COVERAGE" == "0" ]]; then
  summary "_Report-only: no minimum is enforced yet. Set \`COVERAGE_MIN\` in the workflow to gate._"
  exit 0
fi

if awk -v l="$LINE_PCT" -v m="$MIN_COVERAGE" 'BEGIN { exit !(l < m) }'; then
  summary ""
  summary "**Coverage gate failed** — ${LINE_PCT}% is below the required ${MIN_COVERAGE}%."
  exit 1
fi

summary "_Meets the ${MIN_COVERAGE}% minimum._"
