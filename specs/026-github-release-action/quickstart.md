# Quickstart: GitHub Release Action

**Feature**: 026-github-release-action  
**Audience**: Repository maintainers  
**Date**: 2026-03-31

---

## How Releases Work

Releases are published automatically and manually via the `release.yml` GitHub Actions workflow.

---

## Automatic Release (on merge to main)

1. Create and merge a pull request into `main`.
2. The `Android CI` workflow runs automatically.
3. When `Android CI` completes successfully, the `Release` workflow starts.
4. The workflow builds the release APK, publishes a GitHub Release with auto-generated notes, and commits the updated version number back to `main`.
5. The release appears on the **Releases** page: `https://github.com/nielsverhoeven/NDI-for-Android/releases`.

**Nothing else is required.** The version number is managed automatically.

---

## Manual Release (on demand)

1. Go to the repository on GitHub.
2. Click **Actions** → **Release** (in the left sidebar).
3. Click **Run workflow**.
4. Choose the branch and set `prerelease`:
   - Leave `prerelease: true` if you want a test/RC release.
   - Set `prerelease: false` only if releasing from `main` for a production release.
5. Click **Run workflow**.

---

## Verifying a Release

After the workflow completes:

- **Releases page**: `https://github.com/nielsverhoeven/NDI-for-Android/releases`
- Look for the tag `v{versionName}` (e.g., `v0.12.4`).
- Confirm one APK asset is attached: `ndi-for-android-{versionName}.apk`.
- Release notes are auto-populated from merged PRs and commits.

---

## Troubleshooting

### Release did not trigger after a merge to main

- Check **Actions** → **Android CI** → confirm the run completed with status ✅.
- Check **Actions** → **Release** → if it did not start, the CI run likely failed or was skipped.
- If CI passed but Release still did not start, check that `workflow_run` is properly configured in `release.yml`.

### Release failed with "tag already exists"

- This means `v{versionName}` already exists as a release tag.
- Cause: `version.properties` was not committed back after the previous release (bot push was blocked by branch protection).
- Fix: Manually increment the version in `version.properties` (bump `versionCode` and `versionName`), commit and push to `main`, then re-trigger the release via manual dispatch.

### APK not found at expected path

- The `renameReleaseApk` Gradle task must run after `assembleRelease`. Check the Gradle log for errors in that task.
- Expected path: `app/build/outputs/apk/release/ndi-for-android-{versionName}.apk`

### Release build failed (minification/shrinking error)

- The `verifyReleaseHardening` gate runs before the build. If it fails, check that `isMinifyEnabled = true` and `isShrinkResources = true` are still set in `app/build.gradle.kts`.
- ProGuard rule errors appear in the Gradle build log as R8 diagnostics.

### Branch protection blocks the version commit-back

- The release is still published; only the version commit-back step is affected.
- After the release, the bot logs a warning. You must manually commit `version.properties` to `main`.
- To prevent this permanently: add `github-actions[bot]` as a bypass actor in the branch protection rule for `main`.

---

## Branch Protection Setup (one-time)

To allow the workflow to push the version commit back to `main`:

1. Go to **Settings** → **Branches** → **Branch protection rules** → edit the `main` rule.
2. Under **Allow specified actors to bypass required pull requests**, add `github-actions[bot]`.
3. Save changes.

If this is not configured, the release will still succeed but the version commit-back will fail with a warning.

---

## Required Repository Permissions

The workflow uses only the built-in `GITHUB_TOKEN`. No additional secrets are required for the initial scope (debug-signed APK). Ensure the token has **read and write** permissions under **Settings** → **Actions** → **General** → **Workflow permissions**.
