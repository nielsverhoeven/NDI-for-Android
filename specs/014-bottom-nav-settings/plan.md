# Implementation Plan: Bottom Navigation Settings Access

**Branch**: `014-bottom-nav-settings` | **Date**: March 26, 2026 | **Spec**: `/specs/014-bottom-nav-settings/spec.md`
**Input**: Feature specification from `/specs/014-bottom-nav-settings/spec.md`

## Summary

Replace top-right settings entry affordances with a dedicated Settings item in bottom navigation, while preserving single-activity Navigation Component behavior, destination-selected-state synchronization, and deep-link compatibility. Implement navigation wiring and UI updates in presentation/app navigation layers, then validate with failing-test-first JUnit coverage and emulator-run Playwright scenarios plus full regression.

## Technical Context

**Language/Version**: Kotlin 2.2.10, Android XML/View system with Jetpack Navigation  
**Primary Dependencies**: AndroidX Navigation, Lifecycle/ViewModel, Material Components (Material 3), module DI via AppGraph/service-locator dependency providers  
**Storage**: N/A for this feature (no new persistence model; existing Room usage unaffected)  
**Testing**: JUnit unit tests, Android instrumentation where needed, Playwright e2e on emulator (primary acceptance path)  
**Target Platform**: Android API 24+ phones/emulators (compileSdk/targetSdk 34 baseline)  
**Project Type**: Mobile app (multi-module Android)  
**Performance Goals**: One-tap settings open/exit from bottom nav; no duplicate destination pushes; selected state remains synchronized during rapid tab switching and rotation  
**Constraints**: Must keep single-activity navigation, MVVM boundaries, no new permissions/background work, preserve visible settings title, and keep existing deep links (`ndi://viewer/{sourceId}`, `ndi://output/{sourceId}`) functional  
**Scale/Scope**: Top-level navigation behavior across Home/Stream/View/Settings tabs, in-scope surfaces: source list, viewer, output, settings; modules primarily `app` and `feature/ndi-browser/presentation`

## Constitution Check (Pre-Research Gate)

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] MVVM-only presentation logic enforced (no UI/business logic leakage)
- [x] Single-activity navigation compliance maintained
- [x] Repository-mediated data access preserved
- [x] TDD evidence planned (Red-Green-Refactor with failing-test-first path)
- [x] Unit test scope defined using JUnit
- [x] Playwright e2e scope defined for end-to-end flows
- [x] For visual UI additions/changes: emulator Playwright e2e tests are explicitly planned
- [x] For visual UI additions/changes: existing Playwright e2e regression run is explicitly planned
- [x] Material 3 compliance verification planned for UI changes
- [x] Battery/background execution impact evaluated
- [x] Offline-first and Room persistence constraints respected (if applicable)
- [x] Least-permission/security implications documented
- [x] Feature-module boundary compliance documented
- [x] Release hardening validation planned (R8/ProGuard + shrink resources)

## Project Structure

### Documentation (this feature)

```text
specs/014-bottom-nav-settings/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
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

feature/ndi-browser/presentation/
└── src/main/java/com/ndi/feature/ndibrowser/
    ├── source_list/
    ├── viewer/
    ├── output/
    └── settings/

testing/e2e/
├── tests/
└── scripts/
```

**Structure Decision**: Use the existing Android multi-module structure. Constrain feature changes to `app` navigation wiring and `feature/ndi-browser/presentation` UI/navigation handling, with validation in existing unit/instrumentation and Playwright e2e locations.

## Phase 0 Research Plan

- Resolve navigation-selection synchronization approach across deep links, rotation, and rapid tab changes.
- Validate best-practice destination de-duplication for bottom navigation repeated taps.
- Define Playwright acceptance and regression execution strategy for visual navigation changes.

## Phase 1 Design Plan

- Produce `data-model.md` describing navigation state entities, validation invariants, and state transitions.
- Produce `contracts/` UI-navigation contract covering behavior guarantees and automation entry points.
- Produce `quickstart.md` with test-first implementation and validation commands.
- Update agent context via `.specify/scripts/bash/update-agent-context.sh copilot`.

## Constitution Check (Post-Design Re-Evaluation)

- [x] Design keeps presentation decisions in ViewModels/navigation coordinators; no data/business logic moved to UI views.
- [x] Navigation remains single-activity with Navigation Component destinations/deep links.
- [x] No direct data-source access introduced from presentation; repository boundaries untouched.
- [x] Test-first sequence and JUnit scope are explicitly documented in quickstart.
- [x] Emulator Playwright coverage and full regression rerun are explicit contractual requirements.
- [x] Material 3-compatible bottom navigation behavior remains the interaction model.
- [x] No new background jobs/services introduced; battery impact is neutral.
- [x] No new permissions or sensitive data paths introduced.
- [x] Feature stays within existing module boundaries.
- [x] Release hardening validation is retained in quickstart validation steps.

## Complexity Tracking

No constitution violations identified; no exemptions required.
