# Implementation Plan: NDI Screen Share Output Redesign

**Branch**: `017-ndi-screen-output` | **Date**: 2026-03-27 | **Spec**: `specs/017-ndi-screen-output/spec.md`
**Input**: Feature specification from `/specs/017-ndi-screen-output/spec.md`

## Summary

Redesign the Stream/Output destination into a true outgoing NDI screen-share
surface with explicit per-start consent, background continuity, and deterministic
discovery behavior. The implementation keeps existing `Fragment -> ViewModel ->
Repository` boundaries, keeps native NDI send/discovery logic in `ndi/sdk-bridge`,
and formalizes discovery mode behavior as:

- configured and reachable discovery server: register there
- not configured: advertise by mDNS
- configured but unreachable: fail stream start and show actionable error

## Technical Context

**Language/Version**: Kotlin 2.2.10 (Android modules), Java toolchain 21 with Java 17 bytecode targets, TypeScript 5.8.x for Playwright e2e, PowerShell 5.1+ for Windows e2e orchestration  
**Primary Dependencies**: AndroidX Lifecycle/Navigation/Fragment/Activity, Room 2.7.0, Coroutines 1.8.1, Material 1.12.0, JmDNS 3.6.0, NDI Android SDK bridge in `ndi/sdk-bridge`, `@playwright/test` 1.53.x  
**Storage**: Room via `core/database` (`output_session`, `output_configuration`, settings/preferences) and file artifacts under `testing/e2e/artifacts`  
**Testing**: JUnit4 unit tests in feature modules, Android instrumentation tests, Playwright dual-emulator e2e via `testing/e2e` scripts  
**Target Platform**: Android API 24+ app on phone/tablet, Windows-hosted emulator validation and CI  
**Project Type**: Multi-module Android mobile app  
**Performance Goals**: Start to ACTIVE within the spec target window, maintain uninterrupted background stream for >= 5 minutes, and report deterministic failure for unreachable configured discovery server  
**Constraints**: Keep one active output session, preserve 15-second retry window, no new dangerous permissions, explicit consent each new start, maintain release hardening gates (R8/shrink resources), keep existing Playwright regression suite passing  
**Scale/Scope**: One output feature redesign across app wiring + feature domain/data/presentation + native bridge behavior + dual-emulator e2e coverage

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
specs/017-ndi-screen-output/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── ndi-screen-output-contract.md
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
└── presentation/src/main/java/com/ndi/feature/ndibrowser/output/

ndi/sdk-bridge/
└── src/main/java/com/ndi/sdkbridge/NdiNativeBridge.kt

testing/e2e/
├── playwright.config.ts
├── scripts/run-dual-emulator-e2e.ps1
└── tests/
```

**Structure Decision**: Keep the existing feature-first Android modular
structure. No new module is required; behavior is updated through existing
domain/data/presentation boundaries and sdk-bridge/native integration points.

## Phase 0: Research

Research focuses and resolved decisions are documented in
`specs/017-ndi-screen-output/research.md`:

- discovery behavior precedence and failure semantics (including Option C)
- per-session consent reset semantics and stop/start guarantees
- background continuity and notification expectations under Android constraints
- full-screen capture initiation expectations when Android system chooser exists
- test strategy across unit + emulator Playwright + regression suite

All technical-context unknowns are resolved; no `NEEDS CLARIFICATION` items
remain.

## Phase 1: Design & Contracts

Design outputs:

- data model: `specs/017-ndi-screen-output/data-model.md`
- contract: `specs/017-ndi-screen-output/contracts/ndi-screen-output-contract.md`
- quickstart: `specs/017-ndi-screen-output/quickstart.md`

Design intent:

- model stream session, discovery mode, and consent lifecycle explicitly
- constrain state transitions around READY/STARTING/ACTIVE/STOPPING/STOPPED/INTERRUPTED
- codify unreachable configured discovery server as start failure with clear error
- preserve retry window and continuity semantics without bypassing consent

## Constitution Check (Post-Design Re-Evaluation)

- [x] MVVM-only presentation logic enforced in contracts/state model
- [x] Navigation remains single-activity and deep-link based
- [x] Repository-only data access preserved
- [x] Test-first execution path captured (unit + Playwright)
- [x] Playwright coverage for visual changes defined
- [x] Existing Playwright regression run retained as mandatory gate
- [x] Material 3 verification explicitly included in quickstart validation steps
- [x] Battery impact explicitly evaluated (foreground-service continuity only)
- [x] Least-permission approach maintained (MediaProjection consent, no new dangerous permission)
- [x] Feature/module boundaries preserved
- [x] Runtime preflight and environment-block reporting defined

## Phase 2 Readiness

Ready for `/speckit.tasks` after this plan, with all required artifacts created:

- `specs/017-ndi-screen-output/spec.md`
- `specs/017-ndi-screen-output/plan.md`
- `specs/017-ndi-screen-output/research.md`
- `specs/017-ndi-screen-output/data-model.md`
- `specs/017-ndi-screen-output/contracts/ndi-screen-output-contract.md`
- `specs/017-ndi-screen-output/quickstart.md`

## Complexity Tracking

No constitution violations require exceptions.
