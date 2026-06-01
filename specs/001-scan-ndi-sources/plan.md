# Implementation Plan: NDI Source Discovery and Viewing

**Branch**: `001-scan-ndi-sources` | **Date**: 2026-03-15 | **Spec**: `specs/001-scan-ndi-sources/spec.md`
**Input**: Feature specification from `/specs/001-scan-ndi-sources/spec.md`

## Summary

Deliver Android phone/tablet flows to discover NDI sources, select one source,
and view video with resilient interruption handling. The implementation uses
MVVM + repository boundaries, single-activity Navigation Component routing,
Room-backed selection persistence, and strict TDD. Toolchain governance is
explicitly enforced through open blocker `TOOLCHAIN-001` (owner, affected
components, target resolution date, target resolution cycle) until the latest
stable compatible Android baseline is validated with NDI SDK compatibility.

## Technical Context

**Language/Version**: Kotlin 1.9.24 (Android modules target Java 17); Gradle runtime on Android Studio stable JBR 21  
**Primary Dependencies**: AndroidX/Jetpack (Lifecycle, Navigation, Room), Material 3, Kotlin Coroutines, NDI 6 Android SDK via `ndi/sdk-bridge`  
**Storage**: Room for user-critical continuity state and discovery/session metadata  
**Testing**: JUnit (unit and repository contract tests), Playwright for default end-to-end coverage, and release validation with R8/ProGuard enabled  
**Target Platform**: Android API 24+ (phones and tablets)  
**Android Toolchain Baseline**: compileSdk 34 / targetSdk 34, AGP 8.5.2, Gradle 8.7, Kotlin 1.9.24, Java 17 module targets, Android Studio stable JBR 21; uplift tracked by `TOOLCHAIN-001`  
**Project Type**: Feature-modularized Android mobile app with native SDK bridge  
**Performance Goals**: SC-001 discovery <= 5s in >= 90% attempts, SC-002 first visible video <= 3s in >= 90% attempts, SC-003 interruption recovery path available in >= 95% tests  
**Constraints**: No location permission, 5-second foreground-only auto-refresh, bounded reconnect window 15 seconds, no overlapping scans, no autoplay on launch  
**Scale/Scope**: One-source-at-a-time viewer workflow, three user stories (discovery, selection/viewing, interruption recovery), validation on representative phone and tablet devices

## Constitution Check (Pre-Design Gate)

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- MVVM gate: PASS - state/event logic remains in feature ViewModels.
- Navigation gate: PASS - single-activity Navigation Component routes Source List and Viewer.
- Data gate: PASS - repositories mediate discovery, viewer sessions, and persisted selection.
- TDD gate: PASS - tests-first tasks defined for unit, contract, and UI flows.
- UX gate: PASS - Material 3 loading/success/empty/failure and recovery states specified.
- Battery gate: PASS - discovery polling is foreground-only with explicit pause/resume behavior.
- Offline gate: PASS - Room persists last-selected source for continuity.
- Permission gate: PASS - location permission explicitly prohibited.
- Modularity gate: PASS - feature modules + `ndi/sdk-bridge` boundary are preserved.
- Release gate: PASS - release-mode validation with shrinking/optimization is required.
- Platform gate: PASS - API 24+ support and phone/tablet compatibility are required.
- Toolchain gate: PASS WITH BLOCKER - `TOOLCHAIN-001` is open and documented with owner, affected components, target date, and target cycle.

## Project Structure

### Documentation (this feature)

```text
specs/001-scan-ndi-sources/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── ndi-feature-contract.md
├── tasks.md
└── validation/
    └── toolchain-blockers.md
```

### Source Code (repository root)

```text
app/
core/
feature/
ndi/
scripts/
docs/
```

**Structure Decision**: Use the existing feature-modular Android repository
layout with app navigation orchestration in `app`, shared models/persistence in
`core`, feature workflow logic in `feature`, and NDI native/JNI isolation in
`ndi/sdk-bridge`.

## Phase 0: Research Consolidation

Research outputs are finalized in `specs/001-scan-ndi-sources/research.md` and
resolve all clarifications from the technical context:

- Toolchain baseline decision and explicit blocker governance (`TOOLCHAIN-001`).
- NDI SDK isolation through `ndi/sdk-bridge`.
- Foreground-only 5-second discovery cadence plus manual refresh.
- 15-second bounded interruption retry strategy.
- Canonical source identity model (`sourceId` over display name).
- Room-backed offline continuity strategy for last-selected source.
- Strict Red-Green-Refactor test strategy.
- No-location-permission policy.

All research decisions include rationale and alternatives considered.

## Phase 1: Design & Contracts

Design artifacts are finalized and aligned with the specification:

- Data model: `specs/001-scan-ndi-sources/data-model.md`
  - Entities: `NdiSource`, `DiscoverySnapshot`, `ViewerSession`,
    `UserSelectionState`, `ToolchainCompatibilityBlocker`.
- Contract definition: `specs/001-scan-ndi-sources/contracts/ndi-feature-contract.md`
  - Repository, ViewModel, navigation, permission, observability, and
    release-validation contracts.
- Quickstart and validation workflow: `specs/001-scan-ndi-sources/quickstart.md`
  - Prerequisites, module setup, NDI bridge wiring, TDD flow, and release checks.
- Blocker evidence artifact:
  `specs/001-scan-ndi-sources/validation/toolchain-blockers.md`
  - `TOOLCHAIN-001` includes owner, affected components, target resolution date,
    and target resolution cycle.

## Constitution Check (Post-Design Re-Evaluation)

- MVVM gate: PASS - `SourceListViewModel` and `ViewerViewModel` contracts define logic ownership.
- Navigation gate: PASS - route contract includes typed `sourceId` argument to viewer flow.
- Data gate: PASS - repository contracts separate discovery/viewer/selection concerns.
- TDD gate: PASS - tasks define failing tests before implementation in each story.
- UX gate: PASS - Material 3 state expectations are embedded in UI contracts and tests.
- Battery gate: PASS - discovery cadence and interruption behavior are bounded by design.
- Offline gate: PASS - Room persistence and launch preselection/no-autoplay behavior are explicit.
- Permission gate: PASS - permission contract forbids location permission for this feature.
- Modularity gate: PASS - sdk-bridge and feature module boundaries are explicit in plan and tasks.
- Release gate: PASS - release validation contract covers R8/ProGuard and flow checks.
- Platform gate: PASS - API 24+ and phone/tablet validations remain mandatory.
- Toolchain gate: PASS WITH BLOCKER - blocker documentation exists at planning completion and is synchronized with spec/tasks.

## Phase 2 Readiness

Planning artifacts are complete and ready for `/speckit.tasks` execution and
implementation sequencing:

- `specs/001-scan-ndi-sources/spec.md`
- `specs/001-scan-ndi-sources/research.md`
- `specs/001-scan-ndi-sources/data-model.md`
- `specs/001-scan-ndi-sources/contracts/ndi-feature-contract.md`
- `specs/001-scan-ndi-sources/quickstart.md`
- `specs/001-scan-ndi-sources/validation/toolchain-blockers.md`
- `specs/001-scan-ndi-sources/tasks.md`

## Complexity Tracking

No constitution violations require exceptions in this plan. Open toolchain lag
is documented as blocker `TOOLCHAIN-001` and tracked as planned maintenance
work rather than a policy exception.
