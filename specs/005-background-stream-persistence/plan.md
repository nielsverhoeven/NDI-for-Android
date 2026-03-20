# Implementation Plan: Background Stream Persistence

**Branch**: `005-background-stream-persistence` | **Date**: 2026-03-20 | **Spec**: `specs/005-background-stream-persistence/spec.md`
**Input**: Feature specification from `/specs/005-background-stream-persistence/spec.md`

## Summary

Keep NDI output streaming active while the broadcaster app is backgrounded (Home
or other app), and add deterministic dual-emulator Playwright validation that
proves continuity with a real cross-app scenario: start stream on Emulator A,
view on Emulator B, open Chrome on Emulator A, verify Chrome appears on B,
navigate to `https://nos.nl` on A, verify site appears on B.

## Technical Context

**Language/Version**: Kotlin 2.2.10 (Android app modules), TypeScript (Playwright e2e), Gradle Kotlin DSL, Java toolchain 21 with Java 17 bytecode target  
**Primary Dependencies**: AndroidX Fragment/Lifecycle/ViewModel/Navigation, domain repositories in `feature/ndi-browser/domain`, NDI bridge modules in `ndi/sdk-bridge`, Playwright + ADB UI driver utilities in `testing/e2e/tests/support`  
**Storage**: Existing Room-backed persistence via `core/database`; test artifacts in `testing/e2e/artifacts` and `testing/e2e/test-results`  
**Testing**: JUnit4 unit tests for ViewModel/repository behavior and Playwright dual-emulator e2e in `testing/e2e/tests/interop-dual-emulator.spec.ts`  
**Target Platform**: Android API 24+ app runtime and dual-emulator Android validation on Windows host
**Project Type**: Feature-modularized Android mobile application with e2e automation harness  
**Performance Goals**: Preserve active playback continuity in 100% of supported dual-emulator runs; maintain deterministic six-step scenario ordering; keep visual verification window for `nos.nl` within 15 seconds  
**Constraints**: Preserve `Fragment -> ViewModel -> Repository`; keep app composition in `app`; keep domain contracts in `feature/ndi-browser/domain`; no new dangerous permissions; background execution must remain user-initiated, cancellable, and battery-justified  
**Scale/Scope**: One continuity behavior slice in stream lifecycle semantics plus one new/extended dual-emulator e2e scenario and supporting diagnostics

## Constitution Check (Pre-Design Gate)

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- MVVM-only presentation logic: PASS - continuity and navigation behavior remains ViewModel/repository mediated.
- Single-activity navigation architecture: PASS - no deviation from Navigation Component and existing deep-link model.
- Repository-mediated data access: PASS - no direct UI-layer storage or bridge access.
- Strict TDD: PASS - implementation tasks will require failing test additions first (unit + e2e).
- Material 3 compliance: PASS - no new design-system deviation introduced by this behavior feature.
- Battery-conscious execution: PASS WITH JUSTIFICATION - stream continuity while app is backgrounded is explicit user value; work remains active only during user-initiated stream session and must stop on explicit stop action.
- Offline-first reliability: PASS - feature does not add online dependency to core stream state transitions.
- Least-permission security: PASS - no new permissions; MediaProjection consent flow remains mandatory.
- Feature-first modularization: PASS - work stays within existing app/feature/testing module boundaries.
- Release optimization and compatibility: PASS - release hardening and dual-emulator validation remain required evidence.
- Latest-stable toolchain: PASS - no toolchain changes required for this feature.

## Project Structure

### Documentation (this feature)

```text
specs/005-background-stream-persistence/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── ndi-background-stream-persistence-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
app/
├── src/main/java/com/ndi/app/di/AppGraph.kt
├── src/main/java/com/ndi/app/navigation/NdiNavigation.kt
└── src/main/res/navigation/main_nav_graph.xml

feature/ndi-browser/
├── domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt
├── data/src/main/java/com/ndi/feature/ndibrowser/data/
└── presentation/src/main/java/com/ndi/feature/ndibrowser/
    ├── source_list/
    ├── viewer/
    └── output/

core/model/src/main/java/com/ndi/core/model/
└── navigation/TopLevelNavigationModels.kt

testing/e2e/
├── scripts/run-dual-emulator-e2e.ps1
└── tests/
    ├── interop-dual-emulator.spec.ts
    └── support/
        ├── android-device-fixtures.ts
        ├── android-ui-driver.ts
        └── visual-assertions.ts
```

**Structure Decision**: Reuse existing feature-first Android modular structure.
Continuity behavior remains in existing domain/data/presentation boundaries,
and end-to-end validation remains in the unified dual-emulator Playwright suite.

## Phase 0: Research Consolidation

Research results are documented in `specs/005-background-stream-persistence/research.md`.
Key outcomes:

- Explicit continuity policy for user-initiated active output while app is backgrounded.
- Deterministic six-step dual-emulator Chrome/nos.nl verification contract.
- Visual assertion strategy that compares receiver viewer surface against baseline and publisher evidence.
- Fail-fast diagnostics and step-level failure reporting for triage.

All technical context unknowns are resolved; no `NEEDS CLARIFICATION` entries remain.

## Phase 1: Design & Contracts

Design artifacts are complete and aligned to the specification:

- Data model: `specs/005-background-stream-persistence/data-model.md`
  - Defines continuity session state, background transition records, viewer evidence,
    and ordered checkpoint validation entities.
- Contract:
  `specs/005-background-stream-persistence/contracts/ndi-background-stream-persistence-contract.md`
  - Defines stream continuity behavior, ordered six-step e2e scenario, visibility
    assertion guarantees, and failure diagnostics.
- Quickstart:
  `specs/005-background-stream-persistence/quickstart.md`
  - Documents test-first sequence, prerequisites, and local validation commands.
- Agent context update:
  - `.specify/scripts/bash/update-agent-context.sh copilot` executed after artifact generation.

## Constitution Check (Post-Design Re-Evaluation)

- MVVM gate: PASS - design keeps presentation logic in ViewModels and repository contracts.
- Navigation gate: PASS - no architecture split or alternate navigation stack introduced.
- Data gate: PASS - continuity and session metadata remain repository-mediated.
- TDD gate: PASS - design requires failing tests first for continuity and six-step e2e assertions.
- UX gate: PASS - no Material 3 divergence introduced.
- Battery gate: PASS WITH JUSTIFICATION - continuity is tied to explicit active stream session and explicit stop semantics; no autonomous background jobs are introduced.
- Offline gate: PASS - no new mandatory network dependency for local continuity state transitions.
- Permission gate: PASS - existing MediaProjection consent only; no new dangerous permissions.
- Modularity gate: PASS - all touched components stay inside existing modules.
- Release gate: PASS - release hardening and dual-emulator e2e evidence remain completion gates.
- Platform gate: PASS - API/toolchain constraints unchanged.

## Phase 2 Readiness

Planning artifacts prepared for `/speckit.tasks`:

- `specs/005-background-stream-persistence/spec.md`
- `specs/005-background-stream-persistence/plan.md`
- `specs/005-background-stream-persistence/research.md`
- `specs/005-background-stream-persistence/data-model.md`
- `specs/005-background-stream-persistence/contracts/ndi-background-stream-persistence-contract.md`
- `specs/005-background-stream-persistence/quickstart.md`

## Complexity Tracking

No constitution violations require exception handling for this plan.
