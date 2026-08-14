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

# ── Extraction helpers ───────────────────────────────────────────────────────
#
# No `grep … | head` anywhere below. Under `set -euo pipefail`, head closing the pipe early
# sends SIGPIPE to grep, whose non-zero status fails the whole script — which is exactly what
# happened on the first run of this file against a real Cobertura report (a line-rate attribute
# appears on every package and class, so grep was still writing when head exited).
# `grep -m1` stops on its own; `find -print -quit` likewise.

# Prints the value of attr="value" from a single-element string, or nothing.
attr_value() {
  local haystack="$1" attr="$2" match
  match=$(printf '%s' "$haystack" | grep -o "$attr=\"[^\"]*\"" || true)
  match="${match#*\"}"
  printf '%s' "${match%\"}"
}

# ── Test counts (TRX) ────────────────────────────────────────────────────────

TRX=$(find "$RESULTS_DIR" -name '*.trx' -type f -print -quit 2>/dev/null || true)

if [[ -n "$TRX" ]]; then
  COUNTERS=$(grep -o -m1 '<Counters[^/]*/>' "$TRX" || true)
  read_counter() { attr_value "$COUNTERS" "$1"; }

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
    # Parsed as XML rather than grepped: the error message is the thing worth reading, and a
    # name-only list forces whoever is debugging to download the TRX to learn anything.
    # Distinct messages are grouped — a fixture failure fails every test with the same text.
    python3 - "$TRX" <<'PY' > /tmp/failing-tests.txt 2>/dev/null || true
import sys, xml.etree.ElementTree as ET
from collections import OrderedDict

ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
try:
    root = ET.parse(sys.argv[1]).getroot()
except Exception:
    sys.exit(0)

groups = OrderedDict()
for r in root.iter('{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}UnitTestResult'):
    if r.get('outcome') != 'Failed':
        continue
    name = (r.get('testName') or '').strip()
    msg = ''
    for m in r.iter('{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Message'):
        if m.text:
            msg = ' '.join(m.text.split())
            break
    groups.setdefault(msg, []).append(name)

for msg, names in list(groups.items())[:10]:
    if msg:
        print(f"MSG\t{msg[:500]}")
    for n in names[:20]:
        print(f"TEST\t{n}")
    if len(names) > 20:
        print(f"MORE\t… and {len(names) - 20} more with the same failure")
PY
    if [[ -s /tmp/failing-tests.txt ]]; then
      while IFS=$'\t' read -r kind text; do
        case "$kind" in
          MSG)  summary ""; summary "  > $text" ;;
          TEST) summary "- \`$text\`" ;;
          MORE) summary "- _${text}_" ;;
        esac
      done < /tmp/failing-tests.txt
    else
      # Fallback if the TRX schema ever changes under us.
      grep -o 'testName="[^"]*"' "$TRX" 2>/dev/null | sed 's/testName="//;s/"$//' | sort -u > /tmp/names.txt || true
      while read -r n; do [[ -n "$n" ]] && summary "- \`$n\`"; done < /tmp/names.txt
    fi
    summary ""
  fi
else
  summary "No TRX file found under \`$RESULTS_DIR\`."
  summary ""
fi

# ── Coverage (Cobertura) ─────────────────────────────────────────────────────

COVERAGE_FILE=$(find "$RESULTS_DIR" -name 'coverage.cobertura.xml' -type f -print -quit 2>/dev/null || true)

if [[ -z "$COVERAGE_FILE" ]]; then
  exit 0
fi

# Cobertura reports rates as 0..1. Every package and class carries its own line-rate, so take
# the first match only — that is the root <coverage> element, the overall figure.
ROOT_ELEMENT=$(grep -o -m1 '<coverage[^>]*>' "$COVERAGE_FILE" || true)
LINE_RATE=$(attr_value "$ROOT_ELEMENT" 'line-rate')
BRANCH_RATE=$(attr_value "$ROOT_ELEMENT" 'branch-rate')

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
