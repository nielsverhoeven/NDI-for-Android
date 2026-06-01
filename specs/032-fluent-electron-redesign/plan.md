# Implementation Plan: Fluent + Electron UX Redesign

**Branch**: `032-fluent-electron-redesign` | **Date**: 2026-04-27 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from [/specs/032-fluent-electron-redesign/spec.md](./spec.md)

## Summary

Implement a phased Fluent + Electron redesign for complete user flows across Source List, Viewer, Output, Settings, and top-level navigation shell while preserving existing NDI discovery/playback contracts, persistence behavior, and module boundaries. The plan introduces a reusable design token baseline, updates in-scope presentation surfaces flow-by-flow, and enforces compliance through Playwright e2e plus per-screen evidence artifacts under `test-results`.

## Technical Context

**Language/Version**: Kotlin 2.2.10, Android XML/View system + existing UI toolchain, TypeScript 5.x for Playwright validation  
**Primary Dependencies**: AndroidX Fragment/Navigation/Lifecycle/ViewModel, Room, Coroutines/Flow, Material components (mapped to Fluent + Electron intent), NDI bridge in `ndi/sdk-bridge`, Playwright harness in `testing/e2e`  
**Storage**: Existing Room persistence via repository boundaries; no schema additions required for redesign baseline  
**Testing**: JUnit4 module tests, instrumentation where needed, Playwright e2e on emulator(s), preflight scripts in `scripts/`  
**Target Platform**: Android API 24+ (phone + tablet profiles)
**Project Type**: Android multi-module mobile app  
**Performance Goals**: Maintain current core-flow completion performance and preserve SC-004 success threshold (>=95% task completion in validation sample)  
**Constraints**: MVVM-only presentation logic, repository-mediated data access, single-activity navigation, Fluent + Electron compliance evidence required, no mixed legacy/redesigned UI within shipped in-scope flows  
**Scale/Scope**: In-scope surfaces are Source List, Viewer, Output, Settings, and top-level navigation shell with phased rollout by complete flow

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
- [x] Fluent + Electron design-language compliance verification planned for UI changes
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
specs/032-fluent-electron-redesign/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── fluent-electron-redesign-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
app/
├── src/main/java/com/ndi/app/
├── src/main/res/navigation/main_nav_graph.xml
└── src/main/res/

feature/ndi-browser/
├── presentation/src/main/java/com/ndi/feature/ndibrowser/
├── presentation/src/main/res/
├── data/src/main/java/com/ndi/feature/ndibrowser/data/
└── domain/src/main/java/com/ndi/feature/ndibrowser/domain/

feature/theme-editor/
├── presentation/src/main/java/com/ndi/feature/themeeditor/
└── presentation/src/main/res/

core/
├── model/
├── database/
└── testing/

testing/e2e/
├── tests/
├── scripts/
└── package.json

scripts/
├── verify-android-prereqs.ps1
└── verify-e2e-dual-emulator-prereqs.ps1
```

**Structure Decision**: Keep the existing feature-first module architecture. Redesign implementation remains in presentation modules and activity/nav shell wiring, while persistence and behavior contracts remain in existing repositories and domain interfaces.

## Phase 0: Research Plan

Research outcomes in [research.md](./research.md) resolve:

1. Token/system strategy for Fluent + Electron consistency without destabilizing behavior.
2. Flow-based migration strategy that prevents mixed legacy/redesigned UI in shipped flows.
3. Accessibility and adaptive-layout guardrails for phone/tablet profiles.
4. Validation and evidence model for FR-008..FR-014 and SC-001..SC-005.
5. Risk controls for preserving existing discovery, output, and settings behavior semantics.

## Phase 1: Design Outputs

1. Data model documented in [data-model.md](./data-model.md).
2. Interface/test contract documented in [contracts/fluent-electron-redesign-contract.md](./contracts/fluent-electron-redesign-contract.md).
3. Validation flow documented in [quickstart.md](./quickstart.md).

## Post-Design Constitution Check

- [x] Design keeps behavior/persistence policy in existing repositories and ViewModels while limiting redesign to UI presentation contracts
- [x] Single-activity navigation/deep-link contracts remain intact
- [x] Test-first and regression-first workflow remains required
- [x] Visual redesign includes explicit emulator Playwright coverage and full existing Playwright regression run
- [x] Fluent + Electron compliance evidence location and checklist requirements are explicit
- [x] No new permissions or unmanaged background execution paths are introduced

Gate Result (Post-Design): PASS

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | N/A | N/A |

## Phase Outputs

- Phase 0 research: [research.md](./research.md)
- Phase 1 data model: [data-model.md](./data-model.md)
- Phase 1 contract: [contracts/fluent-electron-redesign-contract.md](./contracts/fluent-electron-redesign-contract.md)
- Phase 1 quickstart: [quickstart.md](./quickstart.md)
- Agent context update: run via `.specify/scripts/bash/update-agent-context.sh copilot`
