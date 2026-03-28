# Implementation Plan: Three-Column Settings Layout

**Branch**: `019-settings-three-pane` | **Date**: 2026-03-28 | **Spec**: `specs/019-settings-three-pane/spec.md`
**Input**: Feature specification from `/specs/019-settings-three-pane/spec.md`

## Summary

Implement a three-column Settings experience for wide-layout configurations,
with persistent main navigation in column 1, settings category list in column
2, and adjustable controls for the selected category in column 3. Keep compact
fallback for non-wide layouts, preserve selected category context across layout
switches, and deliver test-first validation with Playwright coverage plus full
regression in line with constitution gates.

## Technical Context

**Language/Version**: Kotlin 2.2.10 for Android modules, TypeScript 5.8.x for Playwright e2e, PowerShell 5.1+ for validation scripts  
**Primary Dependencies**: AndroidX Fragment/Lifecycle/Navigation, Material Components, Coroutines/Flow, existing feature presentation/service-locator wiring, `@playwright/test`  
**Storage**: Existing Room-backed settings persistence paths (no new storage schema required)  
**Testing**: JUnit unit tests, Android instrumentation where needed, Playwright emulator e2e in `testing/e2e`  
**Target Platform**: Android API 24+ app on phone and tablet form factors in emulator/device environments  
**Project Type**: Multi-module Android mobile app  
**Performance Goals**: Settings pane updates feel immediate during category switching and remain smooth during orientation/layout transitions  
**Constraints**: MVVM-only presentation logic, repository-mediated settings access, no new permissions/background jobs, Material 3 compliance, release hardening and full Playwright regression gates  
**Scale/Scope**: Settings UI/navigation updates spanning app navigation and `feature/ndi-browser/presentation` plus related tests and e2e coverage

## Constitution Check (Pre-Design Gate)

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] MVVM-only presentation logic enforced (no UI/business logic leakage)
- [x] Single-activity navigation compliance maintained
- [x] Repository-mediated data access preserved
- [x] TDD evidence planned (Red-Green-Refactor with failing-test-first path)
- [x] Unit test scope defined using JUnit
- [x] Playwright e2e scope defined for end-to-end flows
- [x] For visual UI additions/changes: emulator Playwright e2e tests are explicitly planned
- [x] For visual UI additions/changes: existing Playwright e2e regression run is explicitly planned
- [x] For shared persistence/settings changes: regression tests for state-preservation are explicitly planned
- [x] Material 3 compliance verification planned for UI changes
- [x] Battery/background execution impact evaluated
- [x] Offline-first and Room persistence constraints respected (if applicable)
- [x] Least-permission/security implications documented
- [x] Feature-module boundary compliance documented
- [x] Release hardening validation planned (R8/ProGuard + shrink resources)
- [x] Runtime preflight checks are defined for required emulators/devices/tools before quality gates
- [x] Environment-blocked gate handling and evidence capture plan is defined

## Project Structure

### Documentation (this feature)

```text
specs/019-settings-three-pane/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── settings-three-column-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
app/
├── src/main/java/com/ndi/app/navigation/NdiNavigation.kt
└── src/main/res/navigation/main_nav_graph.xml

feature/ndi-browser/presentation/
├── src/main/java/com/ndi/feature/ndibrowser/settings/
├── src/main/java/com/ndi/feature/ndibrowser/source_list/
└── src/main/java/com/ndi/feature/ndibrowser/viewer/

feature/ndi-browser/domain/
└── src/main/java/com/ndi/feature/ndibrowser/domain/repository/

feature/ndi-browser/data/
└── src/main/java/com/ndi/feature/ndibrowser/data/repository/

testing/e2e/
├── playwright.config.ts
├── tests/
└── scripts/
```

**Structure Decision**: Maintain current feature-first modular Android structure.
Implement this as a presentation-focused settings flow enhancement with minimal
touches to app navigation wiring and no new modules.

## Phase 0: Research

Research outputs are captured in `specs/019-settings-three-pane/research.md`:

- wide-layout trigger strategy and compact fallback behavior
- pane interaction model for navigation, category selection, and detail updates
- state preservation approach across orientation/layout changes
- accessibility and content-density behavior for three-pane layouts
- test-first and Playwright validation strategy for visual changes
- preflight and environment-blocked gate handling

All technical-context unknowns are resolved; no `NEEDS CLARIFICATION` items
remain.

## Phase 1: Design & Contracts

Design artifacts:

- data model: `specs/019-settings-three-pane/data-model.md`
- contract: `specs/019-settings-three-pane/contracts/settings-three-column-contract.md`
- validation quickstart: `specs/019-settings-three-pane/quickstart.md`

Design intent:

- formalize layout-context and settings-pane interaction state
- codify UI and ViewModel contracts for in-place details updates
- preserve existing repository/persistence semantics while changing presentation
- enforce Material 3 consistency and explicit emulator e2e coverage

## Constitution Check (Post-Design Re-Evaluation)

- [x] MVVM-only presentation logic enforced in UI and state contracts
- [x] Navigation remains single-activity and graph/deep-link based
- [x] Repository-only data access preserved in contracts
- [x] Test-first execution path documented (unit + Playwright)
- [x] Visual Playwright coverage for new flows explicitly defined
- [x] Existing Playwright regression run retained as mandatory gate
- [x] Settings persistence regression expectations are explicit
- [x] Material 3 verification included in quickstart
- [x] Battery impact unaffected (layout/presentation-only behavior)
- [x] Least-permission posture maintained (no new dangerous permissions)
- [x] Feature/module boundaries preserved
- [x] Runtime preflight and blocked-gate reporting defined

## Phase 2 Readiness

Ready for `/speckit.tasks` with required planning artifacts present:

- `specs/019-settings-three-pane/spec.md`
- `specs/019-settings-three-pane/plan.md`
- `specs/019-settings-three-pane/research.md`
- `specs/019-settings-three-pane/data-model.md`
- `specs/019-settings-three-pane/contracts/settings-three-column-contract.md`
- `specs/019-settings-three-pane/quickstart.md`

## Complexity Tracking

No constitution violations require exceptions.
