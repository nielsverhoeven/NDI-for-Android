# Implementation Plan: Discovery Service Reliability

**Branch**: `022-harden-discovery-service` | **Date**: 2026-03-29 | **Spec**: `/specs/022-harden-discovery-service/spec.md`
**Input**: Feature specification from `/specs/022-harden-discovery-service/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Improve discovery reliability and diagnosability by adding protocol-level discovery-server connection checks on add/recheck, expanding developer-mode diagnostics, and increasing traceable logging from settings actions through bridge discovery refresh outcomes. The implementation will reuse existing module seams: repository contracts in `feature/ndi-browser/domain`, data behavior in `feature/ndi-browser/data`, presentation behavior in `feature/ndi-browser/presentation`, app wiring in `app`, and discovery bridge behavior in `ndi/sdk-bridge`.

## Technical Context

**Language/Version**: Kotlin 2.2.10 (Android), Java 17 bytecode target, C++ NDK bridge layer in `ndi/sdk-bridge`  
**Primary Dependencies**: AndroidX Lifecycle/ViewModel, Navigation Component, Kotlin Coroutines/Flow, Room, Material 3, NDI bridge (`ndi:sdk-bridge`)  
**Storage**: Room for discovery server entries (`discovery_servers`), existing settings persistence for developer mode state, in-memory diagnostics buffers  
**Testing**: JUnit unit tests (domain/data/presentation), Gradle module test tasks, Playwright emulator e2e + full Playwright regression  
**Target Platform**: Android API 24+ app runtime with NDI Android SDK available in local/dev test environments
**Project Type**: Android multi-module mobile app  
**Performance Goals**: Add/recheck connection result visible within 5 seconds in normal network conditions; no regression in source-list foreground refresh cadence  
**Constraints**: MVVM-only UI logic, repository-mediated data access, protocol-level connectivity validation (not source-presence check), no new background jobs, deterministic environment preflight before e2e  
**Scale/Scope**: Discovery settings surfaces, discovery repository/bridge telemetry path, and developer diagnostics overlays/logs; no new modules

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
- Planned changes remain in existing app/domain/data/presentation/bridge boundaries.

## Project Structure

### Documentation (this feature)

```text
specs/022-harden-discovery-service/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── discovery-service-ui-diagnostics-contract.md
└── tasks.md
```

### Source Code (repository root)
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
│   ├── DiscoveryServerRepositoryImpl.kt
│   ├── NdiDiscoveryConfigRepositoryImpl.kt  ← pre-existing; no new tasks required for this feature
│   ├── NdiDiscoveryRepositoryImpl.kt
│   └── DeveloperDiagnosticsRepositoryImpl.kt
└── presentation/src/main/java/com/ndi/feature/ndibrowser/settings/
  ├── DiscoveryServerSettingsViewModel.kt
  ├── DiscoveryServerSettingsFragment.kt
  ├── SettingsViewModel.kt
  ├── DeveloperOverlayState.kt
  └── DeveloperOverlayRenderer.kt

ndi/sdk-bridge/
├── src/main/java/com/ndi/sdkbridge/NdiNativeBridge.kt
└── src/main/cpp/ndi_bridge.cpp

testing/e2e/
└── (Playwright emulator scenarios + regression suite)
```

**Structure Decision**: Use the existing Android multi-module architecture and extend current discovery/settings flows in place. No new modules are introduced. Domain contracts remain in `feature/ndi-browser/domain`, repository implementations in `feature/ndi-browser/data`, UI interactions in `feature/ndi-browser/presentation`, bridge-level protocol behavior in `ndi/sdk-bridge`, and dependency composition in `app`.

## Phase 0 and Phase 1 Outputs

- Phase 0 research: `/specs/022-harden-discovery-service/research.md`
- Phase 1 data model: `/specs/022-harden-discovery-service/data-model.md`
- Phase 1 contract(s): `/specs/022-harden-discovery-service/contracts/discovery-service-ui-diagnostics-contract.md`
- Phase 1 quickstart: `/specs/022-harden-discovery-service/quickstart.md`
- Agent context update: executed via `.specify/scripts/bash/update-agent-context.sh copilot`

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No constitutional violations or complexity exceptions are required for this feature.
