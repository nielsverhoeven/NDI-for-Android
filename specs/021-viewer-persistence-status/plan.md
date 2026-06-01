# Implementation Plan: Viewer Persistence and Stream Availability Status

**Branch**: `021-viewer-persistence-status` | **Date**: 2026-03-29 | **Spec**: `/specs/021-viewer-persistence-status/spec.md`
**Input**: Feature specification from `/specs/021-viewer-persistence-status/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Add viewer continuity enhancements that (1) persist and restore the last viewed stream with exactly one saved preview frame image and (2) display per-source Previously Connected and current availability status in source list with View Stream disabled when unavailable. Implementation follows existing multi-module boundaries: domain contracts in `feature/ndi-browser/domain`, repository/data persistence in `feature/ndi-browser/data` + `core/database`, dependency wiring in `app`, and UI state/rendering in `feature/ndi-browser/presentation`.

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: Kotlin 2.2.10, Android Gradle Plugin 9.0.x, Gradle 9.x  
**Primary Dependencies**: AndroidX Lifecycle/ViewModel, Navigation, Room, Kotlin Coroutines, Material 3, NDI bridge (`ndi:sdk-bridge`)  
**Storage**: Room (`core/database`) for metadata + app-internal file storage for one persisted preview image  
**Testing**: JUnit4 unit tests, Gradle module tests, Playwright e2e on emulator(s)  
**Target Platform**: Android API 24+ (compile/target per repo baseline)  
**Project Type**: Android multi-module mobile app  
**Performance Goals**: Viewer restore context visible within 1 second; status-to-button consistency >=99 percent across scenarios  
**Constraints**: MVVM-only UI logic, repository-mediated data access, no playback thread blocking for image persistence, bounded storage (single preview image), two-miss availability debounce  
**Scale/Scope**: Source list and viewer flows in `feature:ndi-browser`; single last-viewed context and per-source status markers

## Constitution Check

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

**Gate Status (Pre-Phase 0)**: PASS

**Post-Design Re-Check (after Phase 1 artifacts)**: PASS

Notes:
- No constitutional violations require complexity exceptions.
- No new permissions/background jobs are introduced; availability is derived from existing discovery polling flow.

## Project Structure

### Documentation (this feature)

```text
specs/021-viewer-persistence-status/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── viewer-source-list-ui-contract.md
└── tasks.md
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused options and expand the chosen structure with
  real paths (e.g., apps/admin, packages/something). The delivered plan must
  not include Option labels.
-->

```text
app/
└── src/main/java/com/ndi/app/di/AppGraph.kt

core/
├── database/src/main/java/com/ndi/core/database/NdiDatabase.kt
├── model/
└── testing/

feature/ndi-browser/
├── domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt
├── data/src/main/java/com/ndi/feature/ndibrowser/data/repository/
│   └── NdiViewerRepositoryImpl.kt
└── presentation/src/main/java/com/ndi/feature/ndibrowser/
  ├── source_list/
  │   ├── SourceListScreen.kt
  │   └── SourceListViewModel.kt
  └── viewer/
    ├── ViewerScreen.kt
    └── ViewerViewModel.kt

ndi/sdk-bridge/
└── src/main/java/com/ndi/sdkbridge/

testing/e2e/
└── (Playwright emulator scenarios)
```

**Structure Decision**: Use existing Android feature-first modular architecture with no new modules. Extend current domain/data/presentation seams for `feature:ndi-browser`, keep persistence in `core/database`, and avoid direct presentation persistence access.

## Phase 0 and Phase 1 Outputs

- Phase 0 research: `/specs/021-viewer-persistence-status/research.md`
- Phase 1 data model: `/specs/021-viewer-persistence-status/data-model.md`
- Phase 1 contract(s): `/specs/021-viewer-persistence-status/contracts/viewer-source-list-ui-contract.md`
- Phase 1 quickstart: `/specs/021-viewer-persistence-status/quickstart.md`
- Agent context update: executed via `.specify/scripts/bash/update-agent-context.sh copilot`

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No constitutional violations or exceptional complexity justifications are required for this feature.
