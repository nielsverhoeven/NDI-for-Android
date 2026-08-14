#!/usr/bin/env bash
#
# Computes the next release version and writes it to $GITHUB_OUTPUT.
#
# version.properties supplies the major.minor line and the versionCode floor. The patch number
# comes from the highest existing git tag on that line, so nothing has to commit back to the
# protected main branch to advance a release.
#
# versionCode moves in lockstep with the patch, keeping it monotonic without external state:
#     versionCode = fileVersionCode + (nextPatch - filePatch)
# Bumping major.minor in version.properties re-bases both cleanly.
#
# Outputs: version_name, version_code, tag_name
#
set -euo pipefail

PROPS="${1:-version.properties}"

if [[ ! -f "$PROPS" ]]; then
  echo "Missing $PROPS" >&2
  exit 1
fi

read_prop() {
  grep -E "^$1=" "$PROPS" | head -1 | cut -d= -f2 | tr -d '[:space:]'
}

FILE_NAME=$(read_prop versionName)
FILE_CODE=$(read_prop versionCode)

if [[ -z "$FILE_NAME" || -z "$FILE_CODE" ]]; then
  echo "versionName or versionCode missing from $PROPS" >&2
  exit 1
fi

if [[ ! "$FILE_NAME" =~ ^([0-9]+)\.([0-9]+)\.([0-9]+)$ ]]; then
  echo "versionName '$FILE_NAME' is not major.minor.patch" >&2
  exit 1
fi

MAJOR="${BASH_REMATCH[1]}"
MINOR="${BASH_REMATCH[2]}"
FILE_PATCH="${BASH_REMATCH[3]}"

# Highest patch already released on this major.minor line.
HIGHEST_PATCH=-1
while read -r tag; do
  [[ -z "$tag" ]] && continue
  if [[ "$tag" =~ ^v${MAJOR}\.${MINOR}\.([0-9]+)$ ]]; then
    p="${BASH_REMATCH[1]}"
    (( p > HIGHEST_PATCH )) && HIGHEST_PATCH="$p"
  fi
done < <(git tag --list "v${MAJOR}.${MINOR}.*" 2>/dev/null || true)

if (( HIGHEST_PATCH < 0 )); then
  # No release on this line yet — take the file's patch as-is so a manual major.minor bump
  # publishes exactly the version written in the file.
  NEXT_PATCH="$FILE_PATCH"
else
  NEXT_PATCH=$(( HIGHEST_PATCH + 1 ))
  # Never regress below the file, in case the file was bumped past the tags by hand.
  (( FILE_PATCH > NEXT_PATCH )) && NEXT_PATCH="$FILE_PATCH"
fi

VERSION_NAME="${MAJOR}.${MINOR}.${NEXT_PATCH}"
VERSION_CODE=$(( FILE_CODE + (NEXT_PATCH - FILE_PATCH) ))
TAG_NAME="v${VERSION_NAME}"

echo "version.properties : $FILE_NAME (code $FILE_CODE)"
echo "highest tag patch  : $([[ $HIGHEST_PATCH -lt 0 ]] && echo 'none' || echo "$HIGHEST_PATCH")"
echo "next               : $VERSION_NAME (code $VERSION_CODE), tag $TAG_NAME"

if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  {
    echo "version_name=$VERSION_NAME"
    echo "version_code=$VERSION_CODE"
    echo "tag_name=$TAG_NAME"
  } >> "$GITHUB_OUTPUT"
fi

if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
  {
    echo "### Next version"
    echo ""
    echo "| | |"
    echo "|---|---|"
    echo "| Version name | \`$VERSION_NAME\` |"
    echo "| Version code | \`$VERSION_CODE\` |"
    echo "| Tag | \`$TAG_NAME\` |"
  } >> "$GITHUB_STEP_SUMMARY"
fi
