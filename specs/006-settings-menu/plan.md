# Implementation Plan: Settings Menu and Diagnostic Overlay

**Branch**: `006-settings-menu` | **Date**: 2026-03-20 | **Spec**: `specs/006-settings-menu/spec.md`
**Input**: Feature specification from `/specs/006-settings-menu/spec.md`

## Summary

Implement a dedicated Settings entry point that allows users to configure a
custom NDI discovery server and toggle Developer Mode, plus a top-of-screen
diagnostic overlay that surfaces stream direction/status and redacted runtime
logs. The implementation will keep app boundaries intact (`Fragment -> ViewModel
-> Repository`), apply discovery setting changes immediately (including active
session interruption when required), and preserve deterministic behavior across
source list, viewer, and output screens.

## Technical Context

**Language/Version**: Kotlin 2.2.10 (Android app modules), TypeScript (Playwright e2e), Gradle Kotlin DSL with Java toolchain 21 and Java 17 bytecode target  
**Primary Dependencies**: AndroidX Navigation/Fragment/Lifecycle/ViewModel, Kotlin Coroutines/Flow, Material 3 components, repository contracts in `feature/ndi-browser/domain`, NDI bridge in `ndi/sdk-bridge`  
**Storage**: Existing Room-backed persistence in `core/database` for settings/config state plus in-memory overlay state projection in ViewModels  
**Testing**: JUnit4 unit tests, Android instrumentation/UI tests, Playwright dual-emulator e2e for end-to-end behavior checks  
**Target Platform**: Android API 24+ on single-activity architecture with navigation graph-driven routing  
**Project Type**: Feature-modularized Android mobile application  
**Performance Goals**: Developer overlay visibility/update within 1 second of mode/state changes; stream status refresh reflected within 3 seconds; immediate discovery-setting application within 1 second  
**Constraints**: No direct persistence access from presentation; no new dangerous permissions; redact sensitive log values; preserve deep-link routes; discovery-server changes may interrupt active stream sessions  
**Scale/Scope**: One new settings flow, one persisted discovery configuration model, one persisted developer-mode toggle, and overlay behavior across Source List, Viewer, and Output screens

## Constitution Check (Pre-Design Gate)

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- MVVM-only presentation logic: PASS - settings/overlay state will be managed by ViewModels and repositories; screens stay as render + intent layers.
- Single-activity navigation architecture: PASS - settings route and returns remain in existing nav graph.
- Repository-mediated data access: PASS - persistence and runtime config updates are repository-backed only.
- Strict TDD (non-negotiable): PASS - failing tests first required for settings persistence, immediate apply interruption behavior, and overlay visibility/redaction.
- Material Design 3 compliance: PASS - settings controls and overlay container will follow Material 3 patterns.
- Battery-conscious execution: PASS - no new background workers/services; updates are lifecycle-bound.
- Offline-first data reliability: PASS - settings are local-first and available without network.
- Least-permission security: PASS - no new permissions; overlay redaction prevents sensitive-value leakage.
- Feature-first modularization: PASS - changes stay in existing app/feature/core module boundaries.
- Release-grade optimization & compatibility: PASS - release hardening checks remain mandatory.
- Latest-stable toolchain governance: PASS - no toolchain downgrade or divergence planned.

## Project Structure

### Documentation (this feature)

```text
specs/006-settings-menu/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── ndi-settings-feature-contract.md
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

feature/ndi-browser/
├── domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt
├── data/src/main/java/com/ndi/feature/ndibrowser/data/
└── presentation/src/main/java/com/ndi/feature/ndibrowser/
    ├── source_list/
    ├── viewer/
    ├── output/
    └── settings/

core/
├── database/src/main/java/com/ndi/core/database/
└── model/src/main/java/com/ndi/core/model/

testing/e2e/
├── scripts/run-dual-emulator-e2e.ps1
├── tests/interop-dual-emulator.spec.ts
└── tests/support/
```

**Structure Decision**: Reuse existing feature-first Android modular structure.
Navigation composition remains in `app`; domain repository contracts stay in
`feature/ndi-browser/domain`; implementations stay in
`feature/ndi-browser/data`; UI and overlay composition remain in
`feature/ndi-browser/presentation`; persisted settings remain in `core/database`.

## Phase 0: Research Consolidation

Research output is captured in `specs/006-settings-menu/research.md` and
resolves technical decisions for:

- Settings persistence approach and immediate discovery-apply behavior.
- Discovery input format (`hostname`, `IPv4`, or bracketed `IPv6` with optional `:port` in range `1-65535`) and validation.
- Unreachable-server fallback semantics with visible warning.
- Developer overlay rendering model and lifecycle behavior.
- Sensitive log redaction policy before UI display.

All technical context items are resolved; no `NEEDS CLARIFICATION` entries
remain.

## Phase 1: Design & Contracts

Design artifacts are complete and mapped to the specification:

- Data model: `specs/006-settings-menu/data-model.md`
  - Defines settings config, discovery endpoint, runtime apply state,
    developer overlay state, stream diagnostics, and redacted log entries.
- Contracts: `specs/006-settings-menu/contracts/ndi-settings-feature-contract.md`
  - Defines navigation, settings input/validation behavior, immediate-apply
    discovery semantics, overlay behavior, and redaction guarantees.
- Quickstart: `specs/006-settings-menu/quickstart.md`
  - Documents test-first workflow, local validation commands, and evidence
    collection expectations.
- Agent context update:
  - `.specify/scripts/bash/update-agent-context.sh copilot` executed after
    artifact generation.

## Constitution Check (Post-Design Re-Evaluation)

- MVVM gate: PASS - settings and overlay behavior is modeled as ViewModel state with repository inputs.
- Navigation gate: PASS - settings route/back behavior remains nav graph controlled.
- Data gate: PASS - repository mediation preserved; no direct UI-to-storage access.
- TDD gate: PASS - design requires test-first coverage for persistence, interruption behavior, fallback warnings, and redaction.
- UX gate: PASS - Material 3 settings controls and consistent top-band overlay behavior retained.
- Battery gate: PASS - no persistent/background execution added.
- Offline gate: PASS - settings and overlay state remain local-first.
- Permission gate: PASS - no permission additions; redaction improves least-privilege data exposure.
- Modularity gate: PASS - boundaries align with `app`, `feature`, and `core` modules.
- Release gate: PASS - release hardening and e2e validation remain required completion evidence.
- Platform gate: PASS - API/toolchain baselines unchanged and compliant.

## Phase 2 Readiness

Planning artifacts ready for `/speckit.tasks`:

- `specs/006-settings-menu/spec.md`
- `specs/006-settings-menu/plan.md`
- `specs/006-settings-menu/research.md`
- `specs/006-settings-menu/data-model.md`
- `specs/006-settings-menu/contracts/ndi-settings-feature-contract.md`
- `specs/006-settings-menu/quickstart.md`

## Complexity Tracking

No constitution violations require exception handling for this plan.
