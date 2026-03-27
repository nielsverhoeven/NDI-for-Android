# Implementation Plan: Refine View Screen Controls

**Branch**: `015-refine-view-screen` | **Date**: 2026-03-27 | **Spec**: `specs/015-refine-view-screen/spec.md`
**Input**: Feature specification from `/specs/015-refine-view-screen/spec.md`

## Summary

Refine the View source list UX to prevent accidental interactions and improve
refresh-state clarity by excluding the current device from selectable sources,
enforcing button-only stream opening via per-row "view stream" actions,
removing output-start actions from the view screen, moving refresh control to
the bottom-left, and making refresh behavior explicit (spinner adjacent,
disabled button during refresh, keep previous list visible while refreshing,
show inline non-blocking error on refresh failure).

## Technical Context

**Language/Version**: Kotlin 2.2.10 (Android), Gradle Kotlin DSL, Java toolchain 21 with Java 17 bytecode target  
**Primary Dependencies**: AndroidX Fragment/Lifecycle/ViewModel/Navigation, Material 3 components, Kotlin Coroutines/Flow, existing feature presentation/domain/data modules  
**Storage**: Existing Room persistence in `core/database` for continuity metadata (no schema changes planned)  
**Testing**: JUnit4 unit tests for ViewModel/presentation logic, Android UI/instrumentation checks, Playwright emulator e2e in `testing/e2e`  
**Target Platform**: Android API 24+ app runtime on emulator/device
**Project Type**: Feature-modularized Android mobile application  
**Performance Goals**: Refresh interaction remains responsive with single in-flight refresh action and no additional latency from UI-state guards; zero accidental row-tap navigations in acceptance/e2e flows  
**Constraints**: Preserve `Fragment -> ViewModel -> Repository`; keep navigation/deep-link structure; no direct DB or SDK bridge access from presentation; no new permissions; maintain release hardening and existing e2e regression pass  
**Scale/Scope**: One screen behavior refinement in `feature/ndi-browser/presentation` plus associated tests and e2e validations

## Constitution Check (Pre-Design Gate)

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- MVVM-only presentation logic enforced: PASS - interaction/state rules remain in ViewModel and UI state models.
- Single-activity navigation compliance maintained: PASS - uses existing nav graph and viewer deep-link routing.
- Repository-mediated data access preserved: PASS - no direct data-source calls from UI layer.
- TDD evidence planned (Red-Green-Refactor): PASS - failing tests for new interaction and refresh behavior are required first.
- Unit test scope defined using JUnit: PASS - ViewModel and presentation state transitions covered.
- Playwright e2e scope defined for end-to-end flows: PASS - visual behavior updates map to emulator e2e flows.
- For visual UI additions/changes: emulator Playwright e2e tests explicitly planned: PASS.
- For visual UI additions/changes: existing Playwright e2e regression run explicitly planned: PASS.
- Material 3 compliance verification planned for UI changes: PASS - control placement/disabled states align with Material patterns.
- Battery/background execution impact evaluated: PASS - no new background work, only foreground refresh state handling.
- Offline-first and Room persistence constraints respected: PASS - no persistence model changes or online-only dependency.
- Least-permission/security implications documented: PASS - no permission changes.
- Feature-module boundary compliance documented: PASS - changes remain in existing app/feature/testing modules.
- Release hardening validation planned (R8/ProGuard + shrink resources): PASS - retained in quickstart validation steps.

## Project Structure

### Documentation (this feature)

```text
specs/015-refine-view-screen/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── ndi-view-screen-controls-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
app/
├── src/main/java/com/ndi/app/navigation/NdiNavigation.kt
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
├── tests/
└── tests/support/
```

**Structure Decision**: Reuse existing feature-first Android modular structure.
UI behavior changes stay in `feature/ndi-browser/presentation/source_list`,
navigation remains in `app`, repository contracts remain in domain/data modules,
and visual regression coverage stays in `testing/e2e`.

## Phase 0: Research Consolidation

Research artifacts are captured in `specs/015-refine-view-screen/research.md`.
All technical choices for self-filtering, button-only interaction, refresh-state
UX, and failure messaging are resolved with rationale and alternatives.

No `NEEDS CLARIFICATION` markers remain.

## Phase 1: Design & Contracts

Design artifacts produced and aligned with spec:

- Data model: `specs/015-refine-view-screen/data-model.md`
  - Defines source-row interaction model, refresh state model, and error
    presentation state plus transitions.
- Contract: `specs/015-refine-view-screen/contracts/ndi-view-screen-controls-contract.md`
  - Defines UI behavior contract for source filtering, click targets, refresh,
    and failure handling along with test obligations.
- Quickstart: `specs/015-refine-view-screen/quickstart.md`
  - Documents test-first development path and validation commands.
- Agent context update:
  - `.specify/scripts/bash/update-agent-context.sh copilot` executed after
    artifacts generation.

## Constitution Check (Post-Design Re-Evaluation)

- MVVM-only presentation logic: PASS - contracts keep behavior in ViewModel/state reducers.
- Single-activity navigation architecture: PASS - no new activity/routes outside existing graph.
- Repository-mediated data access: PASS - unchanged data boundary.
- Strict TDD: PASS - quickstart and plan mandate failing tests first.
- Unit/JUnit gate: PASS - unit scope for interaction and refresh states defined.
- Playwright gate: PASS - new visual flows and full existing suite regression explicitly required.
- Material 3 gate: PASS - action affordance, disabled states, and inline feedback remain MD3 compliant.
- Battery gate: PASS - no new background execution introduced.
- Offline-first gate: PASS - behavior remains functional with existing cached list while refresh occurs/fails.
- Least-permission gate: PASS - no manifest permission changes.
- Feature-first modularization gate: PASS - no boundary violations.
- Release hardening gate: PASS - release validation remains in quickstart.

## Phase 2 Readiness

Planning artifacts are complete and ready for `/speckit.tasks`:

- `specs/015-refine-view-screen/spec.md`
- `specs/015-refine-view-screen/plan.md`
- `specs/015-refine-view-screen/research.md`
- `specs/015-refine-view-screen/data-model.md`
- `specs/015-refine-view-screen/contracts/ndi-view-screen-controls-contract.md`
- `specs/015-refine-view-screen/quickstart.md`

## Complexity Tracking

No constitution violations require exception handling for this plan.
