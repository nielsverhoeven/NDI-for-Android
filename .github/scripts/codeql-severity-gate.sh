#!/usr/bin/env bash
#
# Fails the build when CodeQL found high-or-worse issues.
#
#   codeql-severity-gate.sh <sarif-dir>
#
# Reads SARIF produced locally by `github/codeql-action/analyze` with `upload: never`.
#
# Why local SARIF rather than the code-scanning alerts API: this repository has code scanning
# **default setup** enabled, and GitHub rejects SARIF from an advanced configuration while it
# is on ("CodeQL analyses from advanced configurations cannot be processed when the default
# setup is enabled"). No alert from this workflow ever reaches the API, so there is nothing to
# query. The SARIF on disk is the real result of the analysis and is authoritative here.
#
# Severity comes from the `security-severity` property CodeQL attaches to each rule, on the
# CVSS 0–10 scale: 9.0+ critical, 7.0–8.9 high. The gate blocks at >= 7.0.
#
set -euo pipefail

SARIF_DIR="${1:-sarif-results}"
THRESHOLD="${SECURITY_SEVERITY_THRESHOLD:-7.0}"

summary() {
  if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
    echo "$1" >> "$GITHUB_STEP_SUMMARY"
  fi
  echo "$1"
}

SARIF=$(find "$SARIF_DIR" -name '*.sarif' -type f -print -quit 2>/dev/null || true)

if [[ -z "$SARIF" ]]; then
  summary "### CodeQL severity gate"
  summary ""
  summary "No SARIF found under \`$SARIF_DIR\` — the analysis produced no output."
  exit 1
fi

# Rules live on tool.driver and, for query packs, on tool.extensions. Join results to their
# rule by id across both so nothing is missed.
JQ_FINDINGS='
  [ .runs[]
    | (   ( .tool.driver.rules // [] )
        + ( [ (.tool.extensions // [])[] | (.rules // [])[] ] )
      ) as $rules
    | .results[]?
    | . as $r
    | ( $rules[] | select(.id == $r.ruleId) ) as $rule
    | {
        sev:  (($rule.properties["security-severity"] // "0") | tonumber),
        rule: $r.ruleId,
        msg:  ($r.message.text // ""),
        loc:  ( $r.locations[0].physicalLocation.artifactLocation.uri // "unknown" ),
        line: ( $r.locations[0].physicalLocation.region.startLine // 0 )
      }
  ]'

TOTAL=$(jq "[ .runs[].results[]? ] | length" "$SARIF")
BLOCKING=$(jq --argjson t "$THRESHOLD" "$JQ_FINDINGS | map(select(.sev >= \$t)) | length" "$SARIF")

summary "### CodeQL severity gate"
summary ""
summary "Analysed \`$(basename "$SARIF")\` · total results: **$TOTAL** · at or above severity $THRESHOLD: **$BLOCKING**"
summary ""

if [[ "$BLOCKING" -gt 0 ]]; then
  while IFS=$'\t' read -r sev rule loc line msg; do
    [[ -z "$sev" ]] && continue
    summary "- **[$sev]** \`$rule\` — $loc:$line"
    summary "  <br>$msg"
  done < <(jq -r --argjson t "$THRESHOLD" \
    "$JQ_FINDINGS | map(select(.sev >= \$t)) | sort_by(-.sev) | .[] | [(.sev|tostring), .rule, .loc, (.line|tostring), .msg] | @tsv" \
    "$SARIF")

  summary ""
  summary "Build failed: resolve or dismiss these before merging."
  exit 1
fi

summary "No findings at or above severity $THRESHOLD."
