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

### Release failed because signing secrets are missing

- Production mode requires all signing secrets: `RELEASE_KEYSTORE_BASE64`, `RELEASE_KEYSTORE_PASSWORD`, `RELEASE_KEY_ALIAS`, `RELEASE_KEY_PASSWORD`.
- Check **Settings** → **Secrets and variables** → **Actions** and verify all four are configured.
- If `RELEASE_KEYSTORE_BASE64` was copied incorrectly, regenerate it and update the secret.

### Release failed: APK is debug-signed

- This means production mode ran without valid release-signing material.
- Ensure secrets are present and valid, then rerun the workflow.
- The workflow stores apksigner output in `release-diagnostics/apksigner-output.txt` artifact for confirmation.

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

Ensure `GITHUB_TOKEN` has **read and write** permissions under **Settings** → **Actions** → **General** → **Workflow permissions**.

## Required Signing Secrets (for production releases)

Production releases now require a release keystore. Configure these repository secrets:

- `RELEASE_KEYSTORE_BASE64`: Base64-encoded `.jks` or `.keystore` file content
- `RELEASE_KEYSTORE_PASSWORD`: Keystore store password
- `RELEASE_KEY_ALIAS`: Key alias name
- `RELEASE_KEY_PASSWORD`: Key password

Behavior:

- If `prerelease` is `false` (or release mode resolves to production), missing secrets fail the workflow before build.
- If `prerelease` is `true`, missing secrets allow the run but the APK is treated as non-production.

Example (PowerShell) to generate `RELEASE_KEYSTORE_BASE64`:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\path\to\release-keystore.jks"))
```
