# Implementation Plan: NDI Discovery Server Compatibility

**Branch**: `[029-ndi-server-compatibility]` | **Date**: 2026-04-07 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from [/specs/029-ndi-server-compatibility/spec.md](./spec.md)

## Summary

Deliver discovery compatibility support across latest known-good, failing venue,
and all obtainable older NDI Discovery Server versions by adding version-aware
classification, compatibility diagnostics via existing surfaces, and a
repeatable compatibility matrix validation flow that preserves baseline behavior
and prevents false full-success reporting in mixed-version configurations.

## Technical Context

**Language/Version**: Kotlin 2.2.10, Gradle/AGP Android stack (AGP 9.0.1)  
**Primary Dependencies**: AndroidX (Lifecycle, Navigation, Room), Kotlin Coroutines, Material Components, NDI native bridge in [:ndi:sdk-bridge](../../ndi/sdk-bridge)  
**Storage**: Room (existing settings/state paths) plus markdown validation artifacts in [test-results](../../test-results)  
**Testing**: JUnit4 module tests, Android instrumentation where needed, Playwright dual-emulator e2e/regression flows  
**Target Platform**: Android API 24+ app runtime; physical device and emulator validation environments  
**Project Type**: Multi-module Android mobile app  
**Performance Goals**: No regression in discovery and stream-start success against latest known-good baseline; compatibility classification available within normal discovery completion flow  
**Constraints**: Reuse existing diagnostics surfaces (no dedicated compatibility UI), preserve repository/service-locator architecture, preserve existing tests as regression protection first  
**Scale/Scope**: One feature slice spanning discovery/data/presentation coordination, compatibility matrix reporting, and validation evidence for all obtainable in-scope server versions

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] MVVM-only presentation logic enforced (no UI/business logic leakage)
- [x] Single-activity navigation compliance maintained
- [x] Repository-mediated data access preserved
- [x] TDD evidence planned (Red-Green-Refactor with failing-test-first path)
- [x] Existing tests are treated as regression protection first; any edits are limited to directly impacted behavior and explicitly justified
- [x] Unit test scope defined using JUnit
- [x] Playwright e2e scope defined for end-to-end flows
- [x] For visual UI additions/changes: emulator Playwright e2e tests are explicitly planned (conditional only if existing surfaces change)
- [x] For visual UI additions/changes: existing Playwright e2e regression run is explicitly planned (conditional only if existing surfaces change)
- [x] For shared persistence/settings changes: regression tests for state-preservation are explicitly planned (only if touched)
- [x] Material 3 compliance verification planned for UI changes (not expected; fallback gate retained)
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
specs/029-ndi-server-compatibility/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
app/
  src/main/java/com/ndi/app/di/AppGraph.kt
  src/main/res/navigation/main_nav_graph.xml

core/
  model/
  database/

feature/
  ndi-browser/
    domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/
    data/src/main/java/com/ndi/feature/ndibrowser/data/repository/
    presentation/src/main/java/com/ndi/feature/ndibrowser/settings/
    presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/
    presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/

ndi/
  sdk-bridge/src/main/java/com/ndi/sdkbridge/

testing/
  e2e/tests/
  e2e/scripts/

test-results/
```

**Structure Decision**: Use the existing multi-module Android architecture with
changes limited to discovery bridge/repository pipelines, compatibility
classification/diagnostics mapping, and validation/reporting assets under
feature/data/presentation plus test-results evidence.

## Phase 0: Research Plan

Research outputs are documented in [research.md](./research.md) and resolve:

1. NDI Discovery Server version compatibility handling strategy for mixed and
legacy deployments.
2. Classification taxonomy and evidence model alignment (compatible, limited,
incompatible, blocked; temporary unknown).
3. Discovery diagnostics surfacing strategy using existing app surfaces.
4. Validation matrix and blocked-environment reporting best practices.

## Phase 1: Design Outputs

1. Data model documented in [data-model.md](./data-model.md).
2. Interface contracts documented in [contracts/ndi-discovery-compatibility-contract.md](./contracts/ndi-discovery-compatibility-contract.md).
3. Validation and operator flow documented in [quickstart.md](./quickstart.md).

## Post-Design Constitution Check

- [x] Design keeps discovery/reporting behavior in repository and ViewModel layers
- [x] No dedicated new UI flow required; existing diagnostics surfaces retained
- [x] Compatibility classification defined without violating module boundaries
- [x] Test-first and regression-protection policy integrated in validation flow
- [x] Environment preflight + blocked-gate evidence flow explicitly defined

Gate Result (Post-Design): PASS

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | N/A | N/A |
