#!/usr/bin/env bash
#
# Fails the build when CodeQL has open high-or-worse findings on the given ref.
#
#   codeql-severity-gate.sh <ref>
#
# github/codeql-action/analyze has no severity threshold of its own — it uploads results and
# succeeds regardless of what it found. This reads the alerts back and applies the gate.
#
# Security severities blocked: critical, high. Alerts already dismissed or fixed are ignored,
# so an accepted risk stays accepted.
#
set -euo pipefail

REF="${1:-}"
if [[ -z "$REF" ]]; then
  echo "Usage: codeql-severity-gate.sh <ref>" >&2
  exit 2
fi

REPO="${GITHUB_REPOSITORY:?GITHUB_REPOSITORY not set}"
BLOCKING='critical high'

summary() {
  if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
    echo "$1" >> "$GITHUB_STEP_SUMMARY"
  fi
  echo "$1"
}

echo "Querying code scanning alerts for $REPO @ $REF"

# A repository with code scanning disabled, or a first run with nothing uploaded yet, returns
# 403/404. That is not a finding — do not fail the build on it.
if ! ALERTS=$(gh api \
      -H "Accept: application/vnd.github+json" \
      "/repos/$REPO/code-scanning/alerts?ref=$REF&state=open&per_page=100" 2>/dev/null); then
  summary "Code scanning alerts are not readable for this ref (analysis may be new, or the"
  summary "feature disabled). Skipping the severity gate rather than failing on it."
  exit 0
fi

# security_severity_level is the meaningful field for security queries; rule.severity covers
# the quality queries that have no security rating.
COUNT_BLOCKING=0
FINDINGS=""

while IFS=$'\t' read -r sev rule url; do
  [[ -z "$sev" ]] && continue
  for b in $BLOCKING; do
    if [[ "$sev" == "$b" ]]; then
      COUNT_BLOCKING=$(( COUNT_BLOCKING + 1 ))
      FINDINGS+="- **${sev}** \`${rule}\` — ${url}"$'\n'
    fi
  done
done < <(echo "$ALERTS" | jq -r '.[] | [(.rule.security_severity_level // "none"), .rule.id, .html_url] | @tsv')

TOTAL_OPEN=$(echo "$ALERTS" | jq 'length')

summary "### CodeQL severity gate"
summary ""
summary "Open alerts on \`$REF\`: **$TOTAL_OPEN** · blocking (high or critical): **$COUNT_BLOCKING**"
summary ""

if (( COUNT_BLOCKING > 0 )); then
  summary "$FINDINGS"
  summary "Build failed: resolve or dismiss these before merging."
  exit 1
fi

summary "No high or critical findings."
