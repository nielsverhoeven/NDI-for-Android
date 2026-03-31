# Research: GitHub Release Action

**Phase**: 0 — Research  
**Feature**: 026-github-release-action  
**Date**: 2026-03-31

---

## Decision 1: Workflow Trigger Strategy

**Decision**: Use `workflow_run` (triggering after `Android CI` succeeds on `main`) as the primary automatic trigger, plus `workflow_dispatch` for manual on-demand releases.

**Rationale**: `workflow_run` is the only GitHub Actions mechanism that can express a cross-workflow dependency — meaning the release will only publish after `Android CI` has confirmed a successful preflight. Using `push: branches: [main]` alone would race against CI and could publish a broken build. `workflow_dispatch` provides the manual trigger required by FR-002, with an optional `prerelease` boolean input.

**Alternatives considered**:
- `push: branches: [main]` only — rejected because it cannot enforce CI completion before release.
- `push: tags: v*` — rejected because it shifts version management to the developer (requires manual tagging) rather than automating it.
- Reusing CI workflow with a release job appended — rejected because it violates separation of concerns; the release pipeline has different permissions and lifetime than the build/test pipeline.

---

## Decision 2: Version Extraction Strategy

**Decision**: Run `./gradlew assembleRelease` (which increments version.properties during Gradle configuration phase), then read the newly-written `version.properties` to extract `versionName` and `versionCode`. After the release is published, commit the updated `version.properties` back to `main` with `[skip ci]` to ensure the next build starts from the incremented baseline.

**Rationale**: The `incrementAndWriteVersion()` function in `app/build.gradle.kts` runs unconditionally during every Gradle configuration phase. If the updated `version.properties` is not committed back, the next release workflow run will read the same stale values and either produce a duplicate tag (causing a hard failure per FR-011) or publish a release with an incorrect version label. Committing back with `[skip ci]` prevents the Android CI from re-triggering on the bot commit, which would in turn re-trigger the release workflow.

**Alternatives considered**:
- Read version BEFORE the build — rejected because the Gradle build always increments the version, so the pre-build value is already stale the moment the build starts.
- Use a composite tag `v{versionName}+{versionCode}` for uniqueness without commit-back — rejected for this iteration because FR-006 standardizes production tags to `v{versionName}`.
- Write a separate version-bump PR — rejected as unnecessarily complex for a solo-developer workflow.

**Prerequisite documented**: Branch protection rules on `main` must permit `github-actions[bot]` to push (commit-back step). If branch protection blocks this, the workflow logs a warning and release publication still succeeds; maintainers then manually commit `version.properties` to `main`.

---

## Decision 3: APK Discovery Strategy (Constitution Principle XII Compliance)

**Decision**: After `./gradlew assembleRelease`, the `renameReleaseApk` Gradle task renames `app-release.apk` to `ndi-for-android-{versionName}.apk`. The workflow reads `versionName` from the post-build `version.properties` and constructs the deterministic path `app/build/outputs/apk/release/ndi-for-android-${versionName}.apk`. A PowerShell guard verifies the file exists and is non-zero bytes before the release step runs.

**Rationale**: Constitution Principle XII requires deterministic artifact paths. Glob/`find` approaches can match stale or unexpected files. Since `renameReleaseApk` produces a predictably named file and we know the version after the build, we can construct the path without ambiguity.

**Alternatives considered**:
- `find app/build/outputs/apk/release -name "*.apk"` — rejected because it could match stale APKs from a previous run in a cached workspace.
- Uploading via `actions/upload-artifact` + downloading — unnecessary indirection; the file is local to the same job.

---

## Decision 4: Release Notes Generation

**Decision**: Use `softprops/action-gh-release`'s `generate_release_notes: true` flag, which calls the GitHub Releases API to auto-populate release notes from pull requests and commits merged since the previous release tag.

**Rationale**: This is GitHub-native, requires no additional secrets, and produces structured release notes (grouped by PR label if label automation is present). It handles the first-release edge case gracefully (no previous tag → includes all history since repo creation).

**Alternatives considered**:
- Manually maintained `CHANGELOG.md` — rejected per spec assumption; maintainer should not need to author notes manually.
- `git log` between tags passed as body text — produces lower quality, unlabelled content; GitHub's auto-generate is superior.
- `release-drafter` action — overkill for current scope; requires configuration and label setup.

---

## Decision 5: Duplicate Tag Prevention and Failure Mode

**Decision**: `softprops/action-gh-release` fails with a non-zero exit code if the tag already exists in the repository. This satisfies FR-011 without custom logic. Combined with the version commit-back strategy (Decision 2), the tag naturally rotates with each build.

**Rationale**: Let the release action enforce the invariant rather than implementing custom pre-flight tag checks. The cost of a duplicate-tag failure is a clear error log entry; no partial release artifacts are published.

**Alternatives considered**:
- Explicit pre-step `gh release view v{tag} && exit 1` — redundant; the release action already handles this.
- `--overwrite` flag on the release action — explicitly rejected by FR-011.

---

## Decision 6: Pre-Release Detection

**Decision**: When triggered by `workflow_dispatch`, the workflow checks `github.ref`. If not `refs/heads/main`, OR if the optional `prerelease` input is `true`, the release is marked as a GitHub pre-release. Releases triggered by `workflow_run` (which only fires on `main`) are always production releases.

**Rationale**: FR-013 requires non-main manual triggers to be marked as pre-releases. A `workflow_dispatch` `boolean` input also lets main-branch operators opt into pre-release labelling without changing the branch (e.g., for RC testing).

---

## Decision 7: Build Environment (CI Runner)

**Decision**: The release workflow runs on `windows-latest`, consistent with the existing `android-ci.yml`. This ensures the same JDK, Android SDK, and PowerShell environment is available without additional setup.

**Rationale**: Mixing runners (e.g., ubuntu-latest for the release job) would require different SDK setup steps and might introduce path inconsistencies. Consistency with the existing CI reduces maintenance surface.

---

## Decision 8: Signing Approach

**Decision**: Initial release uses the default debug signing that Gradle applies to `assembleRelease` when no keystore is configured. This produces an installable, aligned APK suitable for sideloading. Release keystore signing is explicitly deferred (see spec Assumptions).

**Rationale**: The spec explicitly allows debug-signed packages for the initial deliverable. Introducing keystore secrets adds operational complexity (secret rotation, signing key management) that is out of scope for this feature.

**Alternatives considered**:
- Full keystore signing with stored secret — deferred to a follow-on feature.
- `apksigner` post-processing — unnecessary if Gradle already signs at build time.

---

## Resolved Unknowns Summary

| Unknown | Resolved As |
|---------|------------|
| Workflow trigger mechanism | `workflow_run` + `workflow_dispatch` |
| Version extraction timing | Post-build read of `version.properties` |
| Version persistence | Commit-back to `main` with `[skip ci]` |
| APK discovery | Deterministic path using post-build versionName |
| Release notes source | GitHub native `generate_release_notes: true` |
| Duplicate tag handling | `softprops/action-gh-release` hard-fails on duplicate |
| Pre-release logic | `github.ref != refs/heads/main` OR manual `prerelease` input |
| APK signing | Debug-signed initially; keystore deferred |
| CI runner | `windows-latest` (consistent with android-ci.yml) |
