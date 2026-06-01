# Tasks: GitHub Release Action

**Input**: Design documents from `/specs/026-github-release-action/`  
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/ ✓

**Tests**: This feature is a GitHub Actions YAML workflow (CI/CD infrastructure). Per the documented Constitution Check exception, no JUnit unit-test framework applies to YAML configuration. Validation substitutes: each user story includes a **dry-run execution task** that directly exercises the workflow behaviour before the story is considered complete.

**Deliverable**: One new file — `.github/workflows/release.yml`

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different sections of the YAML file, no intra-phase deps)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Exact file paths are included in every task description

---

## Phase 1: Setup

**Purpose**: Create the workflow file and establish its basic skeleton so all subsequent phases have a concrete file to edit.

- [x] T001 Create `.github/workflows/release.yml` with top-level `name: Release` and an empty `jobs:` block (no triggers yet) in `.github/workflows/release.yml`

**Checkpoint**: File exists at `.github/workflows/release.yml`

---

## Phase 2: Foundational (Shared Infrastructure — Blocking)

**Purpose**: Job-level boilerplate needed by every user story: checkout, Java setup, Android SDK exposure, and preflight verification. Must complete before any story-level implementation begins.

**⚠️ CRITICAL**: All story phases depend on these steps being present in the workflow file.

- [x] T002 Add `release` job with `runs-on: windows-latest` and `permissions: contents: write`, then add `actions/checkout@v4` step with `persist-credentials: true` (required for the version commit-back in T012) in `.github/workflows/release.yml`
- [x] T003 [P] Add `actions/setup-java@v4` step (distribution: temurin, java-version: '21') matching the configuration in `android-ci.yml` in `.github/workflows/release.yml`
- [x] T004 [P] Add PowerShell step to expose `ANDROID_SDK_ROOT=$env:ANDROID_HOME` to `$GITHUB_ENV`, identical to the pattern in `android-ci.yml` in `.github/workflows/release.yml`
- [x] T005 Add Android prerequisites verification step: `pwsh ./scripts/verify-android-prereqs.ps1 -CiMode -AllowMissingNdiSdk` in `.github/workflows/release.yml`
- [x] T014 [P] Add `workflow_dispatch` trigger block with one `boolean` input: `prerelease` (description: "Mark as pre-release", default: `'true'`) in `.github/workflows/release.yml` — moved here from Phase 4 because T013 (US1 dry-run) requires `workflow_dispatch` to exist before it can be executed; the `prerelease` input is extended with detection logic in Phase 4 (T015–T016)

**Checkpoint**: The `release` job skeleton builds on any runner with JDK 21 and the Android SDK available. The `workflow_dispatch` trigger is available for manual dry-run testing.

---

## Phase 3: User Story 1 — Trigger Automated App Release (Priority: P1) 🎯 MVP

**Goal**: When `Android CI` completes successfully on `main`, automatically build the release APK, verify it, publish it as a versioned GitHub Release, and commit the incremented `version.properties` back to `main`.

**Independent Test**: Trigger the workflow via `workflow_dispatch` with `prerelease: true` on this feature branch (T014, now in Phase 2, provides this trigger). Confirm a new release entry appears on the Releases page with one APK asset attached and a `v{versionName}` tag.

### Validation for User Story 1

- [ ] T013 [US1] Validate US1 dry-run — **prerequisite: T014 must be complete** (provides `workflow_dispatch` trigger). Trigger the workflow via `workflow_dispatch` with `prerelease: true` on the feature branch (after T001–T012 are complete), confirm a new release entry appears on the GitHub **Releases** page with exactly one `.apk` asset attached at `https://github.com/nielsverhoeven/NDI-for-Android/releases` and that the workflow log shows no errors. Also verify the duplicate-tag failure mode: re-run the workflow immediately after without bumping `version.properties` and confirm the pipeline fails with a clear duplicate-tag error (no release entry is created or overwritten). Note: T013 exercises the build and publish pipeline; the `workflow_run` automatic-trigger path (FR-001) is validated end-to-end by T022.

### Implementation for User Story 1

- [x] T006 [US1] Add `workflow_run` trigger block: `on.workflow_run.workflows: ['Android CI']`, `types: [completed]`, `branches: [main]`; add `if: github.event.workflow_run.conclusion == 'success'` on the `release` job to enforce the CI dependency per FR-001 and FR-009 in `.github/workflows/release.yml`
- [x] T007 [US1] Add `./gradlew verifyReleaseHardening` step (runs before the build, fails pipeline if `isMinifyEnabled` or `isShrinkResources` are disabled) in `.github/workflows/release.yml`
- [x] T008 [US1] Add `./gradlew assembleRelease` step in `.github/workflows/release.yml`
- [x] T009 [US1] Add post-build version extraction step: PowerShell reads `version.properties`, derives `VERSION_NAME`, `VERSION_CODE`, `TAG_NAME` (`v$VERSION_NAME`), and `APK_PATH` (`app/build/outputs/apk/release/ndi-for-android-$VERSION_NAME.apk`), writes all four to `$GITHUB_ENV` in `.github/workflows/release.yml`
- [x] T010 [US1] Add APK artifact verification step: PowerShell guard confirming `$env:APK_PATH` exists and has length > 0 bytes; if either check fails, write a diagnostic message identifying the expected path vs. actual state and exit with non-zero code, preventing the release step from running (FR-004, FR-005, FR-008) in `.github/workflows/release.yml`
- [x] T011 [US1] Add `softprops/action-gh-release@v2` publish step wired to extracted step outputs (`tag_name`, `name`, `files`) with `fail_on_unmatched_files: true`; release notes generation is enabled in T018. The action hard-fails on duplicate tags, satisfying FR-011, in `.github/workflows/release.yml`
- [x] T012 [US1] Add version commit-back step: configure git user as `github-actions[bot]`; `git add version.properties`; `git commit -m "chore: bump version to ${{ env.VERSION_NAME }} (code ${{ env.VERSION_CODE }}) [skip ci]"`; `git push origin HEAD:main`; add `continue-on-error: true` and a follow-up step that logs a structured warning if the push was rejected (branch protection blocker), so release publication is not blocked by this step per contract invariant in `.github/workflows/release.yml`

**Checkpoint**: After T006–T012 and T013, the build-publish pipeline is verified end-to-end via `workflow_dispatch`. User Story 1 is independently deliverable. The automatic `workflow_run` trigger (FR-001) is validated in Phase 6 by T022.

---

## Phase 4: User Story 2 — Manually Trigger a Release on Demand (Priority: P2)

**Goal**: Allow a maintainer to cut a release from any branch via the Actions tab. Non-main branches and explicit opt-in produce a GitHub pre-release; main-branch manual triggers produce a production release.

**Independent Test**: Trigger the workflow from the Actions tab with `prerelease: true` on this feature branch, confirm a pre-release entry appears. Then trigger with `prerelease: false` on `main`, confirm a production release (no pre-release flag) is published.

### Validation for User Story 2

- [ ] T017 [US2] Validate US2 — trigger `workflow_dispatch` from the GitHub Actions tab: (a) on this feature branch with `prerelease: true` — confirm resulting release has the GitHub pre-release flag set; (b) after merging to `main`, trigger with `prerelease: false` — confirm resulting release has no pre-release flag; record both results

### Implementation for User Story 2

- [x] T015 [US2] Add pre-release detection step: PowerShell computes `IS_PRERELEASE` — set to `'true'` if `github.ref != 'refs/heads/main'` OR if `github.event.inputs.prerelease == 'true'`; write to `$GITHUB_ENV`; this satisfies FR-013 in `.github/workflows/release.yml`
- [x] T016 [US2] Update the `softprops/action-gh-release` step (from T011) to set `prerelease` from `steps.prerelease_mode.outputs.is_prerelease` so the flag flows from the detection step into the release creation call in `.github/workflows/release.yml`

**Checkpoint**: After T015–T017 (with T014 already completed in Phase 2), both manual production and manual pre-release triggers work correctly.

---

## Phase 5: User Story 3 — Release Includes Descriptive Release Notes (Priority: P3)

**Goal**: Every published release entry shows an auto-generated changelog body sourced from merged PRs and commits since the previous release tag. Handles first-release edge case gracefully.

**Independent Test**: Inspect the body of a release published after T018 is complete — confirm it is non-empty and lists merged PRs or commits.

### Validation for User Story 3

- [ ] T019 [US3] Validate US3 — inspect the body of any release published after T018 is applied (via the dry-run or a real merge); confirm the release body is non-empty and contains at least one PR or commit entry auto-generated by GitHub; record as evidence in `test-results/026-release-notes-validation.md`

### Implementation for User Story 3

- [x] T018 [US3] Update the `softprops/action-gh-release` step (from T011) to add `generate_release_notes: true` — GitHub will auto-populate the release body from merged PRs and commits since the previous `v*` tag, satisfying FR-010 and handling the first-release edge case natively in `.github/workflows/release.yml`

**Checkpoint**: After T018 and T019, every release entry has a non-empty auto-generated changelog. All three user stories are complete.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Failure observability, one-time repository setup documentation, and full end-to-end validation.

- [x] T020 [P] Add per-step failure annotations: for the `verifyReleaseHardening` step, APK verification step, and `softprops/action-gh-release` step, add `if: failure()` follow-up steps that echo structured diagnostics (`::error::` GitHub Actions annotation format) identifying which gate failed, the expected state, and the concrete unblocking command — satisfying FR-012 in `.github/workflows/release.yml`
- [ ] T021 [P] Verify repository workflow permissions are set to **Read and Write** under **Settings → Actions → General → Workflow permissions** and confirm `github-actions[bot]` is listed as a branch protection bypass actor for `main`; update `specs/026-github-release-action/quickstart.md` with any corrections found during this check
- [ ] T022 Run full end-to-end validation — this is the explicit validation of the `workflow_run` automatic-trigger path (FR-001, A3): open a pull request from this feature branch to `main`, merge it, observe the `Android CI` workflow complete successfully, then observe the `Release` workflow trigger automatically (verify the `workflow_run.conclusion == 'success'` guard fires correctly); confirm the complete pipeline (checkout → preflight → hardening → build → extract version → verify APK → publish release → commit-back) executes without error and a production release with one APK asset and non-empty release notes appears at `https://github.com/nielsverhoeven/NDI-for-Android/releases`; record the wall-clock time from merge to release-published and confirm it is under 15 minutes (SC-001)

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Setup)       → no dependencies
Phase 2 (Foundational) → depends on Phase 1
Phase 3 (US1)         → depends on Phase 2 — BLOCKS US2 and US3 in practice
Phase 4 (US2)         → depends on Phase 2; integrates with US1 (T016 edits T011's step)
Phase 5 (US3)         → depends on Phase 2; integrates with US1 (T018 edits T011's step)
Phase 6 (Polish)      → depends on US1 + US2 + US3 completion
```

### User Story Dependencies (within the YAML file)

- **US2** shares the `softprops/action-gh-release` step created in US1 (T011). T016 modifies that step—run after T011.
- **US3** also modifies T011's step (T018). If US2 and US3 are implemented in parallel, coordinate edits to avoid conflicts on that step.
- All other tasks are independent within their story.

### Parallel Opportunities

**Phase 2**:
- T003 (Java setup) and T004 (SDK env exposure) → parallel, different YAML sections

**Phase 3 (US1) — implement in order** due to data dependencies between steps:
- T006 → T007 → T008 → T009 → T010 → T011 → T012 (sequential pipeline steps)
- T013 (validation) → after T012

**Phase 4 (US2)**:
- T014 moved to Phase 2; Phase 4 begins at T015
- T015, T016 → sequential (T015 reads `github.ref` and `github.event.inputs.prerelease`; T016 wires `IS_PRERELEASE` into T011's step)

**Phase 5 (US3)**:
- T018 → one-line edit to the release step; parallel with US2 if editing different fields of T011's step

**Phase 6**:
- T020 and T021 → parallel

### Suggested MVP Scope

Implement **Phase 1 + Phase 2 + Phase 3** (T001–T013) for a fully functional automated release pipeline. This alone satisfies FR-001 through FR-012 and SC-001 through SC-004. US2 and US3 may follow in the same PR or a subsequent one.

---

## Parallel Execution Example: User Story 1

```
T001 (file skeleton)
│
├── T002 (checkout + job skeleton)
│   ├── T003 (Java setup)
│   └── T004 (SDK env)  ← T003 and T004 parallel
│       └── T005 (prereq verify)
│           └── T006 (workflow_run trigger)
│               └── T007 (verifyReleaseHardening step)
│                   └── T008 (assembleRelease step)
│                       └── T009 (version extraction)
│                           └── T010 (APK verify)
│                               └── T011 (release publish)
│                                   └── T012 (commit-back)
│                                       └── T013 (dry-run validate)
│
└── T014 (workflow_dispatch trigger — now in Phase 2, done before T013)
```

---

## Summary

| Phase | Tasks | Stories Covered |
|-------|-------|----------------|
| Phase 1: Setup | T001 | — |
| Phase 2: Foundational | T002–T005, T014 | All |
| Phase 3: US1 (Automated Release) | T006–T013 | US1 |
| Phase 4: US2 (Manual Trigger) | T015–T017 | US2 |
| Phase 5: US3 (Release Notes) | T018–T019 | US3 |
| Phase 6: Polish | T020–T022 | All |
| **Total** | **22 tasks** | |

| Metric | Count |
|--------|-------|
| Total tasks | 22 |
| US1 tasks | 8 (T006–T013) |
| US2 tasks | 3 (T015–T017; T014 moved to Phase 2) |
| US3 tasks | 2 (T018–T019) |
| Parallelizable tasks | 5 (marked [P]) |
| MVP scope (US1 only) | T001–T014 (14 tasks) |
