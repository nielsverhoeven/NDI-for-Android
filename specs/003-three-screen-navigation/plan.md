# Implementation Plan: Three-Screen NDI Navigation

**Branch**: `003-three-screen-navigation` | **Date**: 2026-03-16 | **Spec**: `specs/003-three-screen-navigation/spec.md`
**Input**: Feature specification from `/specs/003-three-screen-navigation/spec.md`

## Summary

Introduce a three-destination top-level information architecture (Home, Stream,
View) using adaptive Material 3 navigation (bottom bar on phone, rail on
tablet), while preserving existing viewer and output feature contracts,
deep-link compatibility, no-autoplay continuity, and foreground-only discovery
behavior. The implementation keeps single-activity Navigation composition in
`app`, leaves domain contracts in `feature/ndi-browser/domain`, and models
process-death restore semantics so launcher entry always opens Home while
Recents restore returns to the last top-level destination.

## Technical Context

**Language/Version**: Kotlin 2.2.10 (Android modules, Java 17 bytecode target), XML navigation/resources, Gradle Kotlin DSL; JDK 21 toolchain via wrapper baseline  
**Primary Dependencies**: AndroidX Navigation/Fragment/Lifecycle/ViewModel, Material 3 components, Kotlin Coroutines/Flow, existing feature repositories in `feature/ndi-browser/domain` and implementations in `feature/ndi-browser/data`  
**Storage**: Existing local Room persistence in `core/database` for continuity metadata; transient UI state in ViewModel/saved state; no new remote storage  
**Testing**: JUnit4 unit tests for ViewModels/navigation coordinator logic, Android UI/instrumentation flow validation for top-level destination switching, Playwright-based e2e validation path retained per constitution for cross-flow regression  
**Target Platform**: Android API 24+ phones and tablets using the current repository toolchain baseline on single-activity app architecture  
**Android Toolchain Baseline**: compileSdk/targetSdk 34, minSdk 24, AGP 9.0.0, Gradle 9.2.1, Kotlin 2.2.10, Java toolchain 21, bytecode target 17  
**Project Type**: Feature-modularized Android mobile application  
**Performance Goals**: Top-level destination switches complete without duplicate destination stacking; navigation selection highlighting remains correct across repeated taps and configuration changes; no unnecessary background work beyond existing foreground-scoped discovery  
**Constraints**: Keep composition in `app`; preserve `Fragment -> ViewModel -> Repository`; no direct DB access from presentation; keep deep links `ndi://viewer/{sourceId}` and `ndi://output/{sourceId}`; preserve Stream keep-running and View stop/no-autoplay continuity; keep telemetry non-sensitive  
**Scale/Scope**: One new Home top-level destination, one adaptive shell navigation host, integration of existing Stream (output) and View (source list + viewer) flows, plus process-death/restore behavior across three top-level destinations

## Constitution Check (Pre-Design Gate)

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- MVVM gate: PASS - top-level destination state, dashboard actions, and restore
  decisions are modeled in ViewModel/coordinator layers; fragments remain view
  renderers and intent dispatchers.
- Navigation gate: PASS - architecture remains single-activity with Navigation
  Component; top-level destinations are graph-driven and deep links preserved.
- Data gate: PASS - continuity and status reads stay repository-mediated; no UI
  direct Room/DAO access.
- TDD gate: PASS - plan includes test-first unit + UI flow coverage for Home,
  Stream, View transitions and restore semantics before implementation.
- UX gate: PASS - Material 3 bottom navigation (phone) and navigation rail
  (tablet) are explicit acceptance requirements.
- Battery gate: PASS - no new background scheduler; existing foreground-only
  discovery refresh remains bounded to View visibility.
- Offline gate: PASS - persisted continuity remains local/non-sensitive in
  existing Room-backed patterns.
- Permission gate: PASS - no new dangerous permission required.
- Modularity gate: PASS - work remains in existing modules (`app`,
  `feature/ndi-browser/*`, `core/*`) and keeps `ndi/sdk-bridge` isolated.
- Release gate: PASS - release hardening verification remains mandatory.
- Platform gate: PASS - API 24+ and phone/tablet support remain in scope.

## Project Structure

### Documentation (this feature)

```text
specs/003-three-screen-navigation/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── ndi-three-screen-navigation-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
app/
├── src/main/java/com/ndi/app/
│   ├── MainActivity.kt
│   ├── di/AppGraph.kt
│   └── navigation/NdiNavigation.kt
└── src/main/res/navigation/main_nav_graph.xml

feature/ndi-browser/
├── domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt
├── data/src/main/java/com/ndi/feature/ndibrowser/data/
└── presentation/src/main/java/com/ndi/feature/ndibrowser/
    ├── source_list/
    ├── viewer/
    └── output/

core/
├── model/
└── database/src/main/java/com/ndi/core/database/NdiDatabase.kt

testing/e2e/
└── tests/
```

**Structure Decision**: Reuse the existing feature-first Android modular layout.
Top-level shell/navigation composition lives in `app`, feature behavior remains
in `feature/ndi-browser/*`, persistence stays centralized in `core/database`,
and any native NDI behavior continues isolated in `ndi/sdk-bridge`.

## Phase 0: Research Consolidation

Research outcomes are captured in
`specs/003-three-screen-navigation/research.md` and resolve the primary design
choices:

- Adaptive top-level navigation shell pattern (bottom bar vs rail).
- Deterministic top-level routing with no duplicate destination stacking.
- Process-death and relaunch semantics (launcher Home default vs Recents
  restore last destination).
- Stream/View continuity guarantees during cross-destination navigation.
- Non-sensitive telemetry event coverage for destination changes and failures.

All items from Technical Context are fully resolved without remaining
`NEEDS CLARIFICATION` entries.

## Phase 1: Design & Contracts

Design artifacts are complete for this feature and align with the specification:

- Data model: `specs/003-three-screen-navigation/data-model.md`
  - Defines `TopLevelDestinationState`, `HomeDashboardSnapshot`,
    `NavigationTransitionRecord`, `StreamContinuityState`, and
    `ViewContinuityState` with validation and transitions.
- Contract definition:
  `specs/003-three-screen-navigation/contracts/ndi-three-screen-navigation-contract.md`
  - Defines repository, ViewModel, navigation, observability, and continuity
    contracts for Home/Stream/View top-level behavior.
- Quickstart and validation workflow:
  `specs/003-three-screen-navigation/quickstart.md`
  - Documents prerequisite checks, test-first sequencing, and validation
    commands for phone/tablet navigation behavior.
- Agent context update:
  - `.specify/scripts/bash/update-agent-context.sh copilot` executed after
    Phase 1 artifact generation.

## Constitution Check (Post-Design Re-Evaluation)

- MVVM gate: PASS - top-level shell and restore policies are captured as
  ViewModel/coordinator contracts rather than fragment business logic.
- Navigation gate: PASS - single-activity nav graph remains authoritative,
  includes Home/Stream/View top-level destinations, and retains deep links.
- Data gate: PASS - all persistence/state restore interactions are repository
  mediated and remain module-boundary compliant.
- TDD gate: PASS - explicit failing-test-first plan covers top-level routing,
  launcher/Recents entry behavior, and cross-screen continuity rules.
- UX gate: PASS - Material 3 adaptive navigation, selected-state indication,
  and Home dashboard action affordances are contractually defined.
- Battery gate: PASS - View discovery refresh remains foreground-bound and View
  playback stops when leaving destination.
- Offline gate: PASS - continuity metadata remains local and non-sensitive.
- Permission gate: PASS - no new dangerous permission or sensitive collection.
- Modularity gate: PASS - no feature leakage across module boundaries and no
  native bridge expansion outside `ndi/sdk-bridge`.
- Release gate: PASS - `verifyReleaseHardening` and release assembly remain in
  quickstart validation path.
- Platform gate: PASS - API 24+ and adaptive phone/tablet UI behavior are
  included in validation.

## Phase 2 Readiness

Planning artifacts are complete and ready for `/speckit.tasks`:

- `specs/003-three-screen-navigation/spec.md`
- `specs/003-three-screen-navigation/plan.md`
- `specs/003-three-screen-navigation/research.md`
- `specs/003-three-screen-navigation/data-model.md`
- `specs/003-three-screen-navigation/contracts/ndi-three-screen-navigation-contract.md`
- `specs/003-three-screen-navigation/quickstart.md`
- `specs/003-three-screen-navigation/tasks.md` (generated and reviewed; 48 tasks across 6 phases)

## Complexity Tracking

No constitution violations require exception handling for this plan.
