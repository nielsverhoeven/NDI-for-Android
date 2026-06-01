# Implementation Plan: E2E App Pre-Installation Gate

**Branch**: `011-e2e-app-preinstall` | **Date**: 2026-03-23 | **Spec**: `specs/011-e2e-app-preinstall/spec.md`
**Input**: Feature specification from `/specs/011-e2e-app-preinstall/spec.md`

---

## Summary

Add a mandatory pre-flight gate to the existing dual-emulator Playwright harness so every run builds the latest debug APK in CI, waits up to 60 seconds for each emulator to become ready, installs the APK, verifies the installed version and launchability, and aborts before any test executes if any device fails. The implementation remains infrastructure-only and extends the existing workflow, PowerShell scripts, and Playwright support layer without changing Android UI, navigation, repository, or Room code.

**Clarifications Locked**:
- Default artifact: `:app:assembleDebug` producing `app/build/outputs/apk/debug/app-debug.apk`
- Version identifier: `versionName + versionCode`
- CI integration: extend `.github/workflows/e2e-dual-emulator.yml`, not a separate pipeline
- Per-device budget: single bounded 60-second readiness-plus-install-plus-launch-verification window
- Readiness behavior: wait up to the same 60-second window, then abort with a device-specific not-ready error

---

## Technical Context

**Language/Version**: Kotlin 2.2.10 for Android modules with Java 17 bytecode and JDK 21 toolchain; TypeScript 5.8.x for Playwright support code; PowerShell 5.1+ and `pwsh` for e2e orchestration  
**Primary Dependencies**: AGP 9.0.1, AndroidX/Jetpack modules already in repo, `@playwright/test` 1.53.x, Android SDK CLI tools (`adb`, `aapt`/`aapt2`, `emulator`), GitHub Actions `windows-latest` runner  
**Storage**: Host filesystem JSON/runtime artifacts under `testing/e2e/artifacts/runtime/`; no Room or app database changes  
**Testing**: Playwright support specs and full Playwright regression execution; JUnit4 only if any JVM-side helper is introduced; no PowerShell unit-test framework is added for this feature  
**Target Platform**: Android emulator devices (API 32-35) provisioned by Feature 010, running locally on Windows developer machines and in GitHub Actions Windows runners  
**Project Type**: Android multi-module mobile app with PowerShell- and TypeScript-based e2e infrastructure  
**Performance Goals**: Each device must complete readiness wait, install, version confirmation, and launch verification within 60 seconds; existing e2e pass rates must not regress  
**Constraints**: Infrastructure-only change, no app UI or navigation changes, no build-logic changes beyond invoking `assembleDebug`, abort before any tests on missing artifact/install/readiness/launch failure, idempotent reruns, explicit CI gate ordering, preserve release hardening  
**Scale/Scope**: Two registered emulators by default (`emulator-5554`, `emulator-5556`), one canonical debug APK per run, extensions limited to `testing/e2e/`, `.github/workflows/`, and feature docs/contracts

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] MVVM-only presentation logic enforced: feature is test-harness-only; no view, ViewModel, or presenter logic is introduced
- [x] Single-activity navigation compliance maintained: no navigation graph or route changes
- [x] Repository-mediated data access preserved: no direct DAO/network/platform data access added to app layers
- [x] TDD evidence planned: failing Playwright support spec for pre-install report and behavior is the first implementation step
- [x] Unit test scope defined using JUnit: no PowerShell unit-test layer is planned; any JVM helper added during implementation must use JUnit4
- [x] Playwright e2e scope defined for end-to-end flows: support validation plus existing suites under `testing/e2e/tests/**/*.spec.ts`
- [x] For visual UI additions/changes: emulator Playwright e2e tests are explicitly planned: not applicable, no UI change
- [x] For visual UI additions/changes: existing Playwright e2e regression run is explicitly planned: not applicable to UI, but full existing Playwright regression remains explicitly required by FR-010
- [x] Material 3 compliance verification planned for UI changes: not applicable, no UI change
- [x] Battery/background execution impact evaluated: no new app background work, services, or WorkManager usage introduced
- [x] Offline-first and Room persistence constraints respected: no persisted app data or schema changes
- [x] Least-permission/security implications documented: no new Android permissions, exported components, or external network exposure
- [x] Feature-module boundary compliance documented: no new module; changes stay in existing e2e scripts, workflow, and specs
- [x] Release hardening validation planned: release hardening remains unchanged; plan preserves `verifyReleaseHardening` and existing release checks

**Post-Phase-1 Re-check**: PASS. Design artifacts stay within infrastructure boundaries, avoid non-JUnit unit-test tooling, and explicitly retain the Playwright regression gate.

---

## Project Structure

### Documentation (this feature)

```text
specs/011-e2e-app-preinstall/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── install-script.contract.md
│   └── pre-flight-report.contract.md
└── tasks.md
```

### Source Code (repository root)

```text
.github/
└── workflows/
    └── e2e-dual-emulator.yml                # Existing CI workflow extended with build + pre-install gate

app/
└── build.gradle.kts                         # Source of debug applicationId/version metadata; no logic changes planned

testing/e2e/
├── package.json                             # Existing Playwright scripts; regression entry points
├── playwright.config.ts                     # Existing Playwright configuration
├── scripts/
│   ├── install-app-preinstall.ps1           # New pre-install orchestrator
│   ├── provision-dual-emulator.ps1          # Existing Feature 010 provisioning
│   └── helpers/
│       └── emulator-adb.ps1                 # Existing ADB helper extended for readiness/version/launch checks
├── tests/
│   ├── interop-dual-emulator.spec.ts
│   ├── settings-developer-overlay.spec.ts
│   ├── settings-discovery-config.spec.ts
│   ├── settings-discovery-fallback.spec.ts
│   ├── settings-invalid-discovery-validation.spec.ts
│   ├── settings-navigation-output.spec.ts
│   ├── settings-navigation-source-list.spec.ts
│   ├── settings-navigation-viewer.spec.ts
│   ├── settings-valid-discovery-persistence.spec.ts
│   ├── three-screen-navigation.spec.ts
│   ├── us1-output-navigation.spec.ts
│   ├── us1-start-output.spec.ts
│   ├── us2-output-status.spec.ts
│   ├── us2-stop-output.spec.ts
│   ├── us3-recovery-actions.spec.ts
│   ├── us3-source-loss.spec.ts
│   └── support/
│       ├── app-preinstall.spec.ts           # New failing-first support spec
│       ├── global-setup-dual-emulator.ts    # Existing global setup extended for local pre-install enforcement
│       ├── dual-emulator-provisioning.spec.ts
│       ├── e2e-infrastructure.spec.ts
│       ├── regression-gate.spec.ts
│       └── relay-connectivity.spec.ts
└── artifacts/
    └── runtime/
        └── preinstall-report.json           # New runtime report artifact
```

**Structure Decision**: Reuse the existing dual-emulator infrastructure introduced by Feature 010. Implementation stays inside the existing workflow, PowerShell helpers, and Playwright support/spec folders instead of adding a new Gradle module or a separate automation pipeline.

---

## Design Decisions

### 1. Single Per-Device Budget for Readiness, Install, and Launch Verification

**Decision**: Treat readiness wait, APK installation, post-install version confirmation, and launch verification as one bounded 60-second per-device budget.

**Rationale**:
- Matches FR-008 and FR-011 without creating competing timeout semantics
- Prevents long readiness waits from hiding install or launch slowness
- Produces a single measurable device elapsed time for the report and CI summaries

### 2. Explicit Device States in the Report Contract

**Decision**: Expand the report model to distinguish `UNREACHABLE`, `NOT_READY`, `INSTALL_FAILED`, `VERSION_MISMATCH`, `LAUNCH_FAILED`, `TIMEOUT`, and `PASS`.

**Rationale**:
- Keeps missing artifact, readiness, install, and launch failures operationally distinct
- Makes Playwright assertions and CI failure messages deterministic
- Resolves the earlier ambiguity around abort-before-install and readiness handling

### 3. CI Gate Plus Local Enforcement Without Double Installation

**Decision**: Keep an explicit `Install app on emulators` workflow step for CI and add report-aware enforcement in `global-setup-dual-emulator.ts` for local runs. Global setup reuses a fresh matching report when CI already ran the pre-install step in the same session.

**Rationale**:
- Satisfies FR-009's explicit CI gate ordering requirement
- Preserves SC-001 for local developer runs started directly from Playwright
- Avoids unnecessary duplicate installs when CI already generated a valid report

### 4. Playwright-First TDD, No Pester Layer

**Decision**: Drive implementation from a failing Playwright support spec and existing regression suites; do not introduce Pester-based unit tests.

**Rationale**:
- Aligns with Constitution IV and avoids the prior JUnit/Pester governance conflict
- Keeps the feature test strategy consistent with the repo's existing e2e harness
- Focuses coverage on externally observable harness behavior rather than PowerShell internals

### 5. Version Confirmation Uses Both Artifact Metadata and Device State

**Decision**: Extract expected version metadata from the APK with `aapt`/`aapt2`, then confirm installed values from the emulator with `adb shell dumpsys package` after install.

**Rationale**:
- Detects missing or corrupt artifacts before install starts
- Confirms the device actually received the intended build
- Provides an explicit `versionIdentifier` field for summary/report consumers

---

## Phase Breakdown

**Phase 0: Research**
- Resolve artifact metadata extraction, launch verification signal, timeout budgeting, report location, and CI/local integration behavior
- Eliminate remaining `NEEDS CLARIFICATION` items from the technical context

**Phase 1: Design**
- Define runtime entities and state transitions in `data-model.md`
- Define report and script contracts under `contracts/`
- Capture operator workflow and CI ordering in `quickstart.md`
- Re-check constitution compliance against the finished design

**Phase 2: Task Generation**
- Generate dependency-ordered tasks covering failing-first Playwright support spec, PowerShell helper updates, workflow integration, and full regression validation

**Phase 3: Implementation (next command)**
- Add or extend readiness/version/install/launch helpers
- Add pre-install orchestrator and report generation
- Wire local and CI entry points
- Add support spec and update regression execution

**Phase 4: Validation (post-implementation)**
- Run support specs validating pre-install behavior and report shape
- Run the existing Playwright regression suites to satisfy FR-010
- Verify CI sequencing and artifact collection behavior

---

## Known Constraints & Mitigations

| Constraint | Mitigation |
|-----------|-----------|
| Emulator may be reachable before package manager is fully ready | Readiness check uses a bounded wait and records `NOT_READY` distinctly from `UNREACHABLE` |
| `am start -W` output varies by device image | Treat zero exit plus `Status: ok` as launch success and force-stop afterward |
| Missing artifact abort needs a valid report schema | Contract allows nullable artifact metadata and an empty `devices` array when aborting before install |
| Full regression execution can be expensive in CI | Keep a focused support spec for fast failure and still require the full existing Playwright regression run before completion |
| Workflow already has provisioning and relay steps | Reuse them; do not create a separate e2e pipeline or new emulator lifecycle logic |

---

## Success Metrics (From Spec)

- **SC-001**: Every test run starts from a confirmed fresh install on all target emulators
- **SC-002**: Stale, missing, or incorrect app installations stop causing downstream test failures
- **SC-003**: Each device completes pre-installation within 60 seconds under normal conditions
- **SC-004**: Failure messages are actionable within 5 minutes without raw device-log inspection
- **SC-005**: Existing Playwright suites continue to pass at pre-feature rates
- **SC-006**: Re-running with the same APK remains idempotent

---

## Complexity Tracking

No constitution violations or approved complexity exceptions are required for this design.
