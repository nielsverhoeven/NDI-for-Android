# Implementation Plan: GitHub Release Action

**Branch**: `026-github-release-action` | **Date**: 2026-03-31 | **Spec**: [spec.md](./spec.md)  
**Input**: Feature specification from `/specs/026-github-release-action/spec.md`

## Summary

Add a `release.yml` GitHub Actions workflow that automatically builds a release-configuration APK and publishes it as a versioned GitHub Release whenever `Android CI` succeeds on `main`. The workflow reuses the existing Gradle `assembleRelease` + `renameReleaseApk` tasks and `verifyReleaseHardening` verification gate, reads the post-build version from `version.properties`, and commits the incremented version back to `main` with `[skip ci]`. A `workflow_dispatch` trigger provides on-demand manual releases with pre-release labelling for non-main branches.

## Technical Context

**Language/Version**: Kotlin 2.2.10 (app); YAML (GitHub Actions workflow)  
**Primary Dependencies**: `softprops/action-gh-release@v2`, `actions/checkout@v4`, `actions/setup-java@v4 (temurin/21)`, existing Gradle tasks (`assembleRelease`, `renameReleaseApk`, `verifyReleaseHardening`)  
**Storage**: N/A — no new Room entities; `version.properties` (existing file) is updated in-place and committed back  
**Testing**: No TDD applicable to YAML workflow configuration (see Constitution Check exception below); validation via dry-run execution on a test branch before merge  
**Target Platform**: GitHub Actions runner (`windows-latest`, consistent with `android-ci.yml`)  
**Project Type**: CI/CD automation workflow (adds a single YAML file; no new Gradle modules)  
**Performance Goals**: Release published within 15 minutes of trigger (SC-001); build runtime budget ~10 minutes  
**Constraints**: `GITHUB_TOKEN` only (no additional secrets for initial scope); must not create duplicate tags; release hardening must remain enabled; commit-back requires branch protection bypass for `github-actions[bot]`  
**Scale/Scope**: One new file: `.github/workflows/release.yml`; one existing file modified: `version.properties` (automated)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [N/A] MVVM-only presentation logic enforced — this feature adds no UI code; the workflow is CI/CD infrastructure only
- [N/A] Single-activity navigation compliance maintained — no navigation changes
- [N/A] Repository-mediated data access preserved — no data layer changes
- [EXCEPTION] TDD evidence planned (Red-Green-Refactor with failing-test-first path) — **Justified exception**: GitHub Actions YAML workflow files are infrastructure configuration, not application code. There is no meaningful unit-test framework for YAML configurations. Validation is performed via a dry-run execution of the workflow on a feature branch (`workflow_dispatch` with `prerelease: true`) before merging. This substitutes for the failing-test-first step. **Approval required**: The PR reviewer must explicitly approve this exception in their review comment before the PR is merged (per Constitution Principle IV: "an alternative must be proposed with justification and approval").
- [N/A] Unit test scope defined using JUnit — no Kotlin/Java code added
- [N/A] Playwright e2e scope defined for end-to-end flows — no visual/app behavior changes
- [N/A] Visual UI additions/changes: emulator Playwright e2e — no visual change
- [N/A] Visual UI additions/changes: existing Playwright e2e regression — no visual change
- [N/A] Shared persistence/settings changes: regression tests — no in-app persistence modified
- [N/A] Material 3 compliance verification — no UI changes
- [N/A] Battery/background execution impact — CI/CD only, not in-app
- [N/A] Offline-first and Room persistence constraints — no offline/Room changes
- [x] Least-permission/security implications documented — `contents: write` is the minimum scope required; no additional secrets; `GITHUB_TOKEN` only; documented in contracts/release-workflow-contract.md
- [N/A] Feature-module boundary compliance — no new Gradle modules
- [x] Release hardening validation planned — workflow runs `./gradlew verifyReleaseHardening` before `assembleRelease`; fails if `isMinifyEnabled` or `isShrinkResources` are disabled
- [x] Runtime preflight checks defined — workflow runs `verify-android-prereqs.ps1 -CiMode -AllowMissingNdiSdk` before any build step
- [x] Environment-blocked gate handling defined — failure modes documented in contracts/release-workflow-contract.md; bot-push block logs a warning rather than failing the release publication

## Project Structure

### Documentation (this feature)

```text
specs/026-github-release-action/
├── plan.md                              # This file
├── research.md                          # Phase 0 output ✓
├── data-model.md                        # Phase 1 output ✓
├── quickstart.md                        # Phase 1 output ✓
├── contracts/
│   └── release-workflow-contract.md     # Phase 1 output ✓
└── tasks.md                             # Phase 2 output (/speckit.tasks) ✓
```

### Source Code (repository root)

```text
.github/
└── workflows/
    ├── android-ci.yml    # EXISTING — unchanged; provides the preflight dependency
    └── release.yml       # NEW — the release workflow added by this feature
```

No changes to any `app/`, `core/`, `feature/`, or `ndi/` module source code.  
`version.properties` is modified automatically by the workflow at runtime (not a source code change).

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| TDD exception for YAML workflow | GitHub Actions YAML is infrastructure; no unit-test framework applies | Running `./gradlew test` against YAML is not meaningful; dry-run execution is the correct analogue |
