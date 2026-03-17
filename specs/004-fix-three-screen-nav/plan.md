# Implementation Plan: Three-Screen Navigation Repairs and E2E Compatibility

**Branch**: `004-fix-three-screen-nav` | **Date**: 2026-03-17 | **Spec**: `specs/004-fix-three-screen-nav/spec.md`
**Input**: Feature specification from `/specs/004-fix-three-screen-nav/spec.md`

## Summary

Repair top-level navigation UX and route correctness by enforcing icon mapping,
fixing View -> Viewer navigation and back behavior, and stabilizing active
destination highlighting. In parallel, harden dual-emulator Playwright coverage
with a unified runtime-branching suite that detects Android version per device,
applies version-specific consent handling, supports mixed-version runs,
evaluates a rolling latest-five Android major-version window at runtime, fails
fast on unsupported versions, and caps intentional static inter-step delay at
1 second.

## Technical Context

**Language/Version**: Kotlin 2.2.10 (Android modules), TypeScript (Playwright e2e), Gradle Kotlin DSL, Java toolchain 21 with Java 17 bytecode target  
**Primary Dependencies**: AndroidX Navigation/Fragment/Lifecycle/ViewModel, Material 3 components, Kotlin Coroutines/Flow, Playwright + ADB helpers for Android automation  
**Storage**: Existing local Room persistence in `core/database` for continuity metadata; test artifacts written to `testing/e2e/artifacts` and `testing/e2e/test-results`  
**Testing**: JUnit4 unit tests, Android UI/instrumentation checks, Playwright dual-emulator e2e (`testing/e2e/tests/interop-dual-emulator.spec.ts`)  
**Target Platform**: Android API 24+ app runtime and dual-emulator Android validation on Windows host with ADB/emulator tooling
**Project Type**: Feature-modularized Android mobile application with e2e automation harness  
**Performance Goals**: Correct View routing/back behavior in 100% of regression runs; enforce <=1 second intentional static inter-step waits; improve dual-emulator median runtime by >=25%  
**Constraints**: Keep single-activity nav composition in `app`; preserve `Fragment -> ViewModel -> Repository`; no direct DB access from presentation; maintain deep links; fail fast for unsupported Android versions; no new dangerous permissions  
**Scale/Scope**: Update top-level nav semantics (Home/Stream/View), selected-state and icon rendering, and one cross-device Playwright suite with per-device runtime flow branching

## Constitution Check (Pre-Design Gate)

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- MVVM-only presentation logic: PASS - route/highlight decisions and continuity state remain ViewModel/coordinator driven.
- Single-activity navigation architecture: PASS - all repairs stay in nav graph/top-level destination wiring.
- Repository-mediated data access: PASS - no direct DAO/platform persistence calls from UI.
- Strict TDD: PASS - tasking will require failing tests first for route fix, highlight fix, version gate, and consent flow branching.
- Material 3 compliance: PASS - icon and active destination behavior remains within Material top-level nav patterns.
- Battery-conscious execution: PASS - no new background jobs; e2e timing changes affect test harness only.
- Offline-first reliability: PASS - no network dependency added for app runtime behavior.
- Least-permission security: PASS - no permission expansion; screen-share consent remains MediaProjection-driven.
- Feature-first modularization: PASS - changes stay within existing `app` and `feature/ndi-browser/*` module boundaries.
- Release optimization and compatibility: PASS - release validation remains required (`verifyReleaseHardening`, release assemble, e2e evidence).
- Latest-stable toolchain governance: PASS - no toolchain downgrade; planning retains compatibility checks.

## Project Structure

### Documentation (this feature)

```text
specs/004-fix-three-screen-nav/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── ndi-three-screen-repair-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
app/
├── src/main/java/com/ndi/app/
│   ├── MainActivity.kt
│   ├── navigation/NdiNavigation.kt
│   └── di/AppGraph.kt
└── src/main/res/navigation/main_nav_graph.xml

feature/ndi-browser/
├── domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt
├── data/src/main/java/com/ndi/feature/ndibrowser/data/
└── presentation/src/main/java/com/ndi/feature/ndibrowser/
    ├── source_list/
    ├── viewer/
    └── output/

testing/e2e/
├── scripts/run-dual-emulator-e2e.ps1
├── tests/interop-dual-emulator.spec.ts
└── tests/support/
    ├── android-device-fixtures.ts
    └── android-ui-driver.ts
```

**Structure Decision**: Reuse existing feature-first modular Android layout.
Navigation and top-level selection logic remain in `app` and presentation
modules; domain contracts remain in `feature/ndi-browser/domain`; e2e behavior
selection remains in one unified Playwright suite with helper modules under
`testing/e2e/tests/support`.

## Phase 0: Research Consolidation

Research results are captured in `specs/004-fix-three-screen-nav/research.md`.
Key decisions resolve:

- Route repair for View selection to Viewer and deterministic back behavior.
- Icon mapping and active destination highlight authority.
- Unified e2e suite with runtime Android-version branching per device.
- Rolling latest-five Android major-version support window evaluation strategy.
- Fail-fast unsupported-version behavior and <=1 second inter-step delay policy.

All technical-context unknowns are resolved; no `NEEDS CLARIFICATION` entries
remain for this feature.

## Phase 1: Design & Contracts

Design artifacts are complete and aligned to the spec:

- Data model: `specs/004-fix-three-screen-nav/data-model.md`
  - Defines destination/highlight state, View navigation session, device version
    profile, rolling support window profile, consent flow variant, and timing
    policy.
- Contracts:
  `specs/004-fix-three-screen-nav/contracts/ndi-three-screen-repair-contract.md`
  - Defines navigation, View routing/back behavior, active-highlight rules,
    unified runtime-branching e2e behavior, and unsupported-version fail-fast.
- Quickstart:
  `specs/004-fix-three-screen-nav/quickstart.md`
  - Documents test-first sequence, local validation commands, and dual-emulator
    verification path with version-awareness checks.
- Agent context update:
  - `.specify/scripts/bash/update-agent-context.sh copilot` executed after
    Phase 1 artifact generation.

## Constitution Check (Post-Design Re-Evaluation)

- MVVM gate: PASS - navigation selection/highlight and View route handling remain ViewModel/coordinator responsibilities.
- Navigation gate: PASS - single-activity nav graph remains authoritative; repair targets route mapping and back-stack semantics only.
- Data gate: PASS - repository mediation preserved; no presentation-layer direct storage access introduced.
- TDD gate: PASS - design requires failing tests first for route regression, highlight correctness, version detection, runtime branching, and fail-fast behavior.
- UX gate: PASS - icon semantics and active-state behavior align with Material 3 top-level navigation expectations.
- Battery gate: PASS - no new background execution introduced.
- Offline gate: PASS - no new online dependencies for runtime app behavior.
- Permission gate: PASS - MediaProjection consent flow retained; no dangerous permission expansion.
- Modularity gate: PASS - modifications stay in existing app/feature/testing boundaries.
- Release gate: PASS - release hardening and e2e validation remain explicit completion requirements.
- Platform gate: PASS - API 24+ unchanged for app runtime; e2e support window handled via runtime latest-five evaluation.

## Phase 2 Readiness

Planning artifacts are complete and ready for `/speckit.tasks`:

- `specs/004-fix-three-screen-nav/spec.md`
- `specs/004-fix-three-screen-nav/plan.md`
- `specs/004-fix-three-screen-nav/research.md`
- `specs/004-fix-three-screen-nav/data-model.md`
- `specs/004-fix-three-screen-nav/contracts/ndi-three-screen-repair-contract.md`
- `specs/004-fix-three-screen-nav/quickstart.md`

## Complexity Tracking

No constitution violations require exception handling for this plan.
