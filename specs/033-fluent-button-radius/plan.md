# Implementation Plan: Fluent Button Radius Alignment

**Branch**: `033-fluent-button-radius` | **Date**: 2026-04-27 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from [/specs/033-fluent-button-radius/spec.md](./spec.md)

## Summary

Implement a visual-only Fluent button radius refinement across in-scope top-level flows (Home, Source List, Viewer, Output, Settings) by introducing a strict uniform less-rounded button corner profile and applying it consistently to button variants used in those flows, while preserving all existing behavior contracts and validating through Playwright and regression evidence artifacts.

## Technical Context

**Language/Version**: Kotlin 2.2.10, Android XML/View system, TypeScript 5.x for Playwright e2e checks  
**Primary Dependencies**: AndroidX Fragment/Navigation/Lifecycle/ViewModel, Material components and style overlays mapped to Fluent intent, existing Playwright harness in `testing/e2e`  
**Storage**: Existing Room and settings persistence paths unchanged; no schema changes  
**Testing**: JUnit4 unit tests, Playwright e2e on emulator(s), prerequisite scripts under `scripts/`  
**Target Platform**: Android API 24+ phone/tablet profiles
**Project Type**: Android multi-module mobile app  
**Performance Goals**: No measurable regression in button-driven task completion or UI responsiveness  
**Constraints**: Visual-only scope; strict uniform button radius in included flows; no behavior/navigation/state changes; preserve MVVM/repository boundaries  
**Scale/Scope**: Button styling and related visual contracts across five in-scope flows and their shared style resources

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
- [x] For shared persistence/settings changes: regression tests for state-preservation are explicitly planned (not applicable; no persistence change)
- [x] Fluent + Electron design-language compliance verification planned for UI changes
- [x] Battery/background execution impact evaluated (none introduced)
- [x] Offline-first and Room persistence constraints respected (if applicable)
- [x] Least-permission/security implications documented (none introduced)
- [x] Feature-module boundary compliance documented
- [x] Release hardening validation planned (R8/ProGuard + shrink resources)
- [x] Runtime preflight checks are defined for required emulators/devices/tools before quality gates
- [x] Environment-blocked gate handling and evidence capture plan is defined

Gate Result (Pre-Research): PASS

## Project Structure

### Documentation (this feature)

```text
specs/033-fluent-button-radius/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── fluent-button-radius-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
app/
├── src/main/res/values/
├── src/main/res/values-night/
└── src/main/java/com/ndi/app/navigation/

feature/ndi-browser/presentation/
├── src/main/res/layout/
├── src/main/res/values/
├── src/main/res/values-night/
└── src/main/java/com/ndi/feature/ndibrowser/

testing/e2e/
├── tests/
└── scripts/

scripts/
├── verify-android-prereqs.ps1
└── verify-e2e-dual-emulator-prereqs.ps1
```

**Structure Decision**: Reuse existing Android multi-module architecture and constrain changes to presentation/style resources plus test/evidence artifacts. No data/domain module contract changes are planned.

## Phase 0: Research Plan

Research outcomes in [research.md](./research.md) resolve:

1. How to enforce one strict button corner profile across in-scope flows.
2. How to apply visual-only changes without behavioral side effects.
3. Which validation model proves Fluent alignment and regression safety.
4. How to handle edge cases (disabled/loading states, dense layouts, dark/light themes).

## Phase 1: Design Outputs

1. Data model documented in [data-model.md](./data-model.md).
2. Contract documented in [contracts/fluent-button-radius-contract.md](./contracts/fluent-button-radius-contract.md).
3. Validation/verification steps documented in [quickstart.md](./quickstart.md).

## Post-Design Constitution Check

- [x] Design remains visual-only and preserves behavior contracts
- [x] No new background work, permissions, or persistence schema changes introduced
- [x] Playwright + existing regression execution remain explicit release gates
- [x] Preflight and environment-blocked classification workflow is explicit
- [x] Fluent + Electron compliance evidence requirements are concrete and auditable

Gate Result (Post-Design): PASS

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | N/A | N/A |

## Phase Outputs

- Phase 0 research: [research.md](./research.md)
- Phase 1 data model: [data-model.md](./data-model.md)
- Phase 1 contract: [contracts/fluent-button-radius-contract.md](./contracts/fluent-button-radius-contract.md)
- Phase 1 quickstart: [quickstart.md](./quickstart.md)
- Agent context update: run via `.specify/scripts/bash/update-agent-context.sh copilot`
