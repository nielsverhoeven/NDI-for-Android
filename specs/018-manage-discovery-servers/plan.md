# Implementation Plan: Discovery Server Settings Management

**Branch**: `018-manage-discovery-servers` | **Date**: 2026-03-28 | **Spec**: `specs/018-manage-discovery-servers/spec.md`
**Input**: Feature specification from `/specs/018-manage-discovery-servers/spec.md`

## Summary

Introduce a dedicated discovery-server management submenu in Settings that lets
users add multiple servers with separate hostname-or-ip and port inputs, apply
default port 5959 when omitted, and enable or disable each server
independently. Runtime selection behavior uses ordered failover over enabled
servers. Implementation keeps existing `Fragment -> ViewModel -> Repository`
boundaries, persists settings via Room-backed repository paths, and enforces
constitution-required test-first plus Playwright visual regression gates.

## Technical Context

**Language/Version**: Kotlin 2.2.10 for Android modules, TypeScript 5.8.x for Playwright e2e, PowerShell 5.1+ for Windows validation scripts  
**Primary Dependencies**: AndroidX Fragment/Lifecycle/Navigation, Material Components, Room, Coroutines, NDI SDK bridge module, `@playwright/test`  
**Storage**: Room in `core/database` for persisted settings and discovery server collection  
**Testing**: JUnit unit tests, Android instrumentation where needed, Playwright emulator e2e in `testing/e2e`  
**Target Platform**: Android API 24+ app, validated on emulator/device and Windows-hosted dev/CI environments  
**Project Type**: Multi-module Android mobile app  
**Performance Goals**: Settings save/update feedback remains near-immediate, persisted state survives restart, failover attempts occur deterministically in configured order  
**Constraints**: MVVM-only presentation logic, repository-mediated data access, no new dangerous permissions, release hardening gates preserved, visual change requires Playwright coverage + full regression  
**Scale/Scope**: One settings feature extension across app navigation wiring, feature presentation/domain/data, Room persistence, and e2e plus unit validation

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
specs/018-manage-discovery-servers/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── discovery-server-management-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
app/
├── src/main/java/com/ndi/app/di/AppGraph.kt
├── src/main/java/com/ndi/app/navigation/NdiNavigation.kt
└── src/main/res/navigation/main_nav_graph.xml

core/
├── model/
├── database/
└── testing/

feature/ndi-browser/
├── domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt
├── data/src/main/java/com/ndi/feature/ndibrowser/data/repository/
└── presentation/src/main/java/com/ndi/feature/ndibrowser/settings/

testing/e2e/
├── playwright.config.ts
├── tests/
└── scripts/
```

**Structure Decision**: Keep existing feature-first Android modular structure.
No new Gradle module is needed; discovery-server management is implemented in
existing app + feature domain/data/presentation boundaries and validated through
existing e2e infrastructure.

## Phase 0: Research

Research outputs are captured in `specs/018-manage-discovery-servers/research.md`:

- persistence model choice for ordered multi-server settings
- host/port validation and default port 5959 strategy
- uniqueness rule and duplicate handling
- ordered failover runtime behavior over enabled entries
- architecture and navigation placement decisions
- test-first and Playwright validation strategy
- preflight and environment-blocked gate handling

All technical-context unknowns are resolved; no `NEEDS CLARIFICATION` items
remain.

## Phase 1: Design & Contracts

Design artifacts:

- data model: `specs/018-manage-discovery-servers/data-model.md`
- contract: `specs/018-manage-discovery-servers/contracts/discovery-server-management-contract.md`
- validation quickstart: `specs/018-manage-discovery-servers/quickstart.md`

Design intent:

- model per-entry settings, ordered collection semantics, and add/edit draft validation
- codify runtime resolution outcomes (success, all enabled unreachable, no enabled)
- preserve backward-safe settings behavior while enabling multi-server persistence
- keep UI flow material-compliant and architecture-aligned with existing settings patterns

## Constitution Check (Post-Design Re-Evaluation)

- [x] MVVM-only presentation logic enforced in UI and state contracts
- [x] Navigation remains single-activity and graph/deep-link based
- [x] Repository-only data access preserved in contracts
- [x] Test-first execution path documented (unit + Playwright)
- [x] Visual Playwright coverage for new flows explicitly defined
- [x] Existing Playwright regression run retained as mandatory gate
- [x] Settings persistence regression expectations are explicit
- [x] Material 3 verification included in quickstart
- [x] Battery impact unaffected (settings-only UI and persistence changes)
- [x] Least-permission posture maintained (no new dangerous permissions)
- [x] Feature/module boundaries preserved
- [x] Runtime preflight and blocked-gate reporting defined

## Phase 2 Readiness

Ready for `/speckit.tasks` with required planning artifacts present:

- `specs/018-manage-discovery-servers/spec.md`
- `specs/018-manage-discovery-servers/plan.md`
- `specs/018-manage-discovery-servers/research.md`
- `specs/018-manage-discovery-servers/data-model.md`
- `specs/018-manage-discovery-servers/contracts/discovery-server-management-contract.md`
- `specs/018-manage-discovery-servers/quickstart.md`

## Complexity Tracking

No constitution violations require exceptions.
