# Implementation Plan: Theme Editor Settings

**Branch**: `016-json-shortname-settings` | **Date**: 2026-03-27 | **Spec**: `c:/gitrepos/NDI-for-Android/specs/016-json-shortname-settings/spec.md`
**Input**: Feature specification from `c:/gitrepos/NDI-for-Android/specs/016-json-shortname-settings/spec.md`

## Summary

Add a settings-based theme editor that lets users select Light/Dark/System mode
and choose a curated accessible accent palette (6-8 options), with immediate
application and persistence across app restarts. To satisfy Constitution
Principle IX, implementation introduces a dedicated `theme-editor` feature
module set (`domain`, `data`, `presentation`) with explicit APIs, while app
composition continues in `app` and shared persistence remains in `core:database`.

## Technical Context

**Language/Version**: Kotlin 2.2.10 (Android, AGP 9.x baseline)  
**Primary Dependencies**: AndroidX Fragment/ViewModel/Lifecycle, Material 3, Coroutines/Flow, Navigation Component, Room  
**Storage**: Room-backed settings persistence in `core:database`  
**Testing**: JUnit4 unit tests, Android instrumentation tests, Playwright e2e emulator suite  
**Target Platform**: Android API 24+ (single-activity app)
**Project Type**: Modular Android mobile app  
**Performance Goals**: Theme mode/accent updates should apply in-session without noticeable navigation or render lag  
**Constraints**: Maintain MVVM and repository boundaries, preserve existing System mode semantics, no new runtime permissions, keep release hardening and Playwright regression gates  
**Scale/Scope**: Introduce a new feature module family for theme editing, integrate with existing settings route and app host theme application

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
- [x] Material 3 compliance verification planned for UI changes
- [x] Battery/background execution impact evaluated (none added)
- [x] Offline-first and Room persistence constraints respected
- [x] Least-permission/security implications documented (no new permissions)
- [x] Feature-module boundary compliance documented (new `theme-editor` modules)
- [x] Release hardening validation planned (R8/ProGuard + shrink resources)

## Project Structure

### Documentation (this feature)

```text
c:/gitrepos/NDI-for-Android/specs/016-json-shortname-settings/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── settings-theme-editor-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
app/
└── src/main/java/com/ndi/app/
    ├── MainActivity.kt
    └── di/AppGraph.kt

core/model/
└── src/main/java/com/ndi/core/model/NdiSettingsModels.kt

core/database/
└── src/main/java/com/ndi/core/database/

feature/theme-editor/domain/
└── src/main/java/com/ndi/feature/themeeditor/domain/

feature/theme-editor/data/
└── src/main/java/com/ndi/feature/themeeditor/data/

feature/theme-editor/presentation/
└── src/main/java/com/ndi/feature/themeeditor/

feature/ndi-browser/presentation/
└── src/main/java/com/ndi/feature/ndibrowser/settings/

testing/e2e/
└── tests/
```

**Structure Decision**: Introduce `feature/theme-editor/{domain,data,presentation}`
as the explicit module boundary for this new feature, and keep existing
`feature/ndi-browser` settings entry as a navigation host/integration point.
Data ownership remains repository-mediated and backed by existing Room tables.

## Phase 0: Research Consolidation

Research output in `c:/gitrepos/NDI-for-Android/specs/016-json-shortname-settings/research.md` resolves:

- theme-mode representation and app-wide application strategy,
- curated accent palette model and accessibility constraints,
- persistence/default normalization approach,
- module boundary strategy to satisfy Constitution Principle IX,
- test strategy aligned to TDD and Playwright visual quality gates.

No unresolved `NEEDS CLARIFICATION` items remain.

## Phase 1: Design & Contracts

Design outputs generated:

- Data model: `c:/gitrepos/NDI-for-Android/specs/016-json-shortname-settings/data-model.md`
- Contract: `c:/gitrepos/NDI-for-Android/specs/016-json-shortname-settings/contracts/settings-theme-editor-contract.md`
- Quickstart: `c:/gitrepos/NDI-for-Android/specs/016-json-shortname-settings/quickstart.md`

Agent context update command:

- `bash ./.specify/scripts/bash/update-agent-context.sh copilot`

## Constitution Check (Post-Design Re-Evaluation)

- [x] MVVM-only presentation logic retained through ViewModel-driven state/actions.
- [x] Single-activity navigation preserved; settings flow remains nav-graph based.
- [x] Repository-mediated access maintained via theme-editor domain contracts.
- [x] Failing-test-first path defined for unit, instrumentation, and Playwright tests.
- [x] Unit and Playwright scopes explicitly cover visual behavior changes.
- [x] Material 3 compliance maintained for settings/theme-editor UI components.
- [x] No additional battery/background work introduced.
- [x] Offline-first persistence remains Room-backed and local-first.
- [x] No permission expansion required.
- [x] Feature-first modularization satisfied by dedicated `theme-editor` modules.
- [x] Release hardening validation retained in quickstart.

## Complexity Tracking

No constitution violations requiring exception approval.
