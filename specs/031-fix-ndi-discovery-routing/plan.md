# Implementation Plan: NDI Discovery Routing Reliability

**Branch**: `031-fix-ndi-discovery-routing` | **Date**: 2026-04-26 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from [/specs/031-fix-ndi-discovery-routing/spec.md](./spec.md)

## Summary

Implement deterministic per-run discovery mode selection so runs use either multicast/mDNS (no enabled servers) or discovery-server-only (one or more enabled servers), enforce a hard 5-second discovery-server timeout with explicit diagnostics and no same-run fallback, and persist/merge discovered sources into the cache using canonical identity updates that preserve continuity metadata. The implementation stays within existing `feature:ndi-browser` repository and Room boundaries and adds targeted unit/e2e validation for FR-001..FR-019 and SC-001..SC-005.

## Technical Context

**Language/Version**: Kotlin 2.2.10, Android Gradle Plugin 9.0.1, Gradle 9.x, Java toolchain 21 (bytecode target 17)  
**Primary Dependencies**: AndroidX Lifecycle/ViewModel, Navigation Component, Room 2.7.0, Coroutines/Flow 1.8.1, Material, JmDNS 3.6.0, `ndi/sdk-bridge` native bridge  
**Storage**: Room (`core/database`) cached/discovery tables and app-internal preview file paths  
**Testing**: JUnit4 module tests, Android instrumentation where needed, Playwright e2e in `testing/e2e`, dual-emulator harness scripts  
**Target Platform**: Android API 24+ on emulator/device
**Project Type**: Android multi-module mobile app  
**Performance Goals**: Discovery-server runs typically complete in 2 seconds (SC-002 target), successful runs complete within 5 seconds hard boundary (FR-005/SC-002), cached rows available before live discovery completion (SC-004)  
**Constraints**: MVVM-only UI logic, repository-mediated data access, single-activity navigation, no same-run multicast fallback in discovery-server mode, offline-first cache continuity, release hardening must remain enabled  
**Scale/Scope**: Changes span `feature:ndi-browser:{domain,data,presentation}`, `core:database` persistence path reuse, `app` dependency wiring, and `testing/e2e` discovery validation coverage

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
specs/031-fix-ndi-discovery-routing/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── ndi-discovery-routing-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
app/
└── src/main/java/com/ndi/app/di/AppGraph.kt

core/
└── database/src/main/java/com/ndi/core/database/NdiDatabase.kt

feature/ndi-browser/
├── domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt
├── data/src/main/java/com/ndi/feature/ndibrowser/data/repository/
│   ├── NdiDiscoveryRepositoryImpl.kt
│   ├── NdiDiscoveryConfigRepositoryImpl.kt
│   └── CachedSourceRepositoryImpl.kt
└── presentation/src/main/java/com/ndi/feature/ndibrowser/
    ├── source_list/
    └── settings/

ndi/sdk-bridge/
└── src/main/java/com/ndi/sdkbridge/NdiNativeBridge.kt

testing/
└── e2e/
```

**Structure Decision**: Keep the existing feature-first Android modular architecture. Discovery mode logic and timeout enforcement live in `feature:ndi-browser:data` repositories, domain contracts remain in `feature:ndi-browser:domain`, persistence remains in `core:database`, and UI consumes repository outputs via existing ViewModels.

## Phase 0: Research Plan

Research outcomes in [research.md](./research.md) resolve:

1. Per-run discovery mode selection pattern and configuration read timing.
2. Timeout enforcement location and diagnostics model for discovery-server mode.
3. Canonical identity cache-merge semantics for endpoint conflicts.
4. Regression and e2e test seams covering FR-011..FR-015 and SC-001..SC-005.
5. Constitution and architecture constraints that gate implementation choices.

## Phase 1: Design Outputs

1. Data model documented in [data-model.md](./data-model.md).
2. Interface/test contract documented in [contracts/ndi-discovery-routing-contract.md](./contracts/ndi-discovery-routing-contract.md).
3. Validation flow documented in [quickstart.md](./quickstart.md).

## Post-Design Constitution Check

- [x] Design keeps all discovery/cache policy in repositories and DAOs, with ViewModels rendering state only
- [x] Navigation remains single-activity with existing deep links and destinations
- [x] Test-first plan covers new behavior before implementation and preserves existing regressions
- [x] Visual behavior updates include emulator Playwright coverage and full regression rerun planning
- [x] Shared persistence behavior includes explicit regression coverage for cache continuity/preserved metadata
- [x] No additional permissions or long-running background jobs are introduced

Gate Result (Post-Design): PASS

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | N/A | N/A |

## Phase Outputs

- Phase 0 research: [research.md](./research.md)
- Phase 1 data model: [data-model.md](./data-model.md)
- Phase 1 contract: [contracts/ndi-discovery-routing-contract.md](./contracts/ndi-discovery-routing-contract.md)
- Phase 1 quickstart: [quickstart.md](./quickstart.md)
- Agent context update: run via `.specify/scripts/bash/update-agent-context.sh copilot`
