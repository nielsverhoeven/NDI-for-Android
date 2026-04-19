# Implementation Plan: Persistent Source Cache

**Branch**: `030-persist-source-cache` | **Date**: 2026-04-19 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from [/specs/030-persist-source-cache/spec.md](./spec.md)

## Summary

Add a Room-backed cached source registry that survives app restarts and updates, keeps a retained preview reference plus discovery-server provenance per source, exposes cached source state immediately while live validation runs, disables View actions until validation completes, and adds a read-only developer database inspection surface inside Settings. The design extends existing `feature:ndi-browser` repository flows, existing Room persistence, and existing developer diagnostics/settings surfaces instead of introducing parallel storage or direct UI-to-database access.

## Technical Context

**Language/Version**: Kotlin 2.2.10 on the current Android Gradle Plugin 9.x / Gradle 9.x toolchain  
**Primary Dependencies**: AndroidX Lifecycle/ViewModel, Navigation Component, Room, Kotlin Coroutines/Flow, Material 3, NDI native bridge in `ndi/sdk-bridge`  
**Storage**: Room in `core/database` for cached-source metadata and associations; app-internal file storage for retained preview image files referenced by Room  
**Testing**: JUnit4 unit tests, targeted Android instrumentation only if needed, Playwright emulator e2e, existing dual-emulator regression harness  
**Target Platform**: Android API 24+ app runtime on emulator/device  
**Project Type**: Android multi-module mobile app  
**Performance Goals**: Cached sources appear on View/Home entry before live validation completes; validation-state button enablement remains consistent across refreshes; preview retention stays bounded and non-blocking  
**Constraints**: MVVM-only presentation logic, repository-mediated data access, Room-backed offline-first persistence, discovery servers used only for source metadata, canonical identity = stable SDK/source ID when available else normalized source endpoint, developer inspection remains read-only  
**Scale/Scope**: One feature slice spanning `core:database`, `feature:ndi-browser:{domain,data,presentation}`, `app` dependency wiring, and emulator validation flows for cached-source/view/settings behavior

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] MVVM-only presentation logic enforced (no UI/business logic leakage)
- [x] Single-activity navigation compliance maintained
- [x] Repository-mediated data access preserved
- [x] TDD evidence planned (Red-Green-Refactor with failing-test-first path)
- [x] Existing tests are treated as regression protection first; any edits are limited to directly impacted behavior and explicitly justified
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

Gate Result (Pre-Research): PASS

## Project Structure

### Documentation (this feature)

```text
specs/030-persist-source-cache/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── persistent-source-cache-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
app/
├── src/main/java/com/ndi/app/di/AppGraph.kt
└── src/main/res/navigation/main_nav_graph.xml

core/
├── database/src/main/java/com/ndi/core/database/NdiDatabase.kt
├── model/src/main/java/com/ndi/core/model/
└── testing/

feature/ndi-browser/
├── domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt
├── data/src/main/java/com/ndi/feature/ndibrowser/data/repository/
│   ├── NdiDiscoveryRepositoryImpl.kt
│   ├── HomeDashboardRepositoryImpl.kt
│   ├── DeveloperDiagnosticsRepositoryImpl.kt
│   └── NdiViewerRepositoryImpl.kt
└── presentation/src/main/java/com/ndi/feature/ndibrowser/
    ├── home/
    ├── source_list/
    ├── settings/
    └── viewer/

testing/
└── e2e/
```

**Structure Decision**: Use the existing Android feature-first modular architecture with no new modules. Add cache persistence in `core/database`, extend domain contracts in `feature:ndi-browser:domain`, implement merge/validation behavior in `feature:ndi-browser:data`, and render cached-state plus developer inspection UI through existing `home`, `source_list`, and `settings` presentation flows.

## Phase 0: Research Plan

Research outputs are documented in [research.md](./research.md) and resolve:

1. How cached discovered sources, preview references, and discovery-server provenance should be persisted without duplicating existing continuity tables.
2. How cached-state validation should be modeled so the UI can distinguish retained context from currently available sources.
3. How stream startup should resolve source endpoints when discovery servers are enabled without ever using discovery-server addresses as stream targets.
4. How developer inspection can reuse existing diagnostics/settings patterns while remaining read-only and constitution-compliant.

## Phase 1: Design Outputs

1. Data model documented in [data-model.md](./data-model.md).
2. Interface and UI contract documented in [contracts/persistent-source-cache-contract.md](./contracts/persistent-source-cache-contract.md).
3. Validation flow documented in [quickstart.md](./quickstart.md).

## Post-Design Constitution Check

- [x] Design keeps persistence and merge logic in repositories and DAOs, with ViewModels consuming modeled state only
- [x] Navigation remains single-activity and uses existing destinations/surfaces
- [x] Shared persistence changes are covered by explicit regression-test planning
- [x] UI changes in Home/View/Settings include Playwright and Material 3 verification planning
- [x] No new permissions, background jobs, or direct DB editing surfaces are introduced

Gate Result (Post-Design): PASS

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | N/A | N/A |

## Phase Outputs

- Phase 0 research: [research.md](./research.md)
- Phase 1 data model: [data-model.md](./data-model.md)
- Phase 1 contract: [contracts/persistent-source-cache-contract.md](./contracts/persistent-source-cache-contract.md)
- Phase 1 quickstart: [quickstart.md](./quickstart.md)
- Agent context update: run via `.specify/scripts/bash/update-agent-context.sh copilot`
