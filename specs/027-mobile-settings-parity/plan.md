# Implementation Plan: Mobile Settings Parity

**Branch**: `027-mobile-settings-parity` | **Date**: 2026-04-07 | **Spec**: `specs/027-mobile-settings-parity/spec.md`
**Input**: Feature specification from `/specs/027-mobile-settings-parity/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Deliver phone-scale parity for recent settings improvements so mobile users can access the same settings capabilities as tablet users without clipped controls, missing sections, or orientation regressions. The approach reuses existing settings architecture (single-activity navigation, MVVM ViewModels, repository-backed persistence), updates phone layout behavior in presentation modules, and validates with required preflight checks plus Playwright emulator coverage on two phone profiles and one tablet profile.

## Technical Context

**Language/Version**: Kotlin 2.2.10, Android XML resources, TypeScript for Playwright e2e  
**Primary Dependencies**: AndroidX Fragment/Navigation/Lifecycle, Material 3, Room (existing persistence path), kotlinx.coroutines, Playwright test harness under `testing/e2e`  
**Storage**: Existing Room settings preference row (`settings_preference.id=1`) via repository abstractions  
**Testing**: JUnit4 module tests, Android instrumentation (where needed), Playwright e2e (`npm --prefix testing/e2e run ...`) with preflight scripts  
**Target Platform**: Android API 24+ app runtime; emulator validation profiles for phone and tablet
**Project Type**: Android multi-module mobile app  
**Performance Goals**: Preserve current settings interaction responsiveness and keep parity scenarios passing at 100% across required profiles (SC-003)  
**Constraints**: Must preserve MVVM + repository boundaries, no new permissions, no schema changes, no background-work expansion, and release hardening checks remain enabled  
**Scale/Scope**: Settings menu presentation parity for phone form factors, orientation handling, and regression-safe validation for existing settings flows

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

Post-design re-check: PASS, contingent on execution of explicit constitution-mandated tasks in `tasks.md` (Material 3 compliance evidence and Espresso-to-Playwright conversion for touched settings coverage).

## Project Structure

### Documentation (this feature)

```text
specs/027-mobile-settings-parity/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── mobile-settings-parity-ui-contract.md
└── tasks.md
```

### Source Code (repository root)
```text
app/
├── src/main/java/com/ndi/app/
├── src/main/res/navigation/main_nav_graph.xml
└── build.gradle.kts

feature/ndi-browser/
├── presentation/src/main/java/com/ndi/feature/ndibrowser/
├── presentation/src/main/res/
├── data/src/main/java/com/ndi/feature/ndibrowser/data/
└── domain/src/main/java/com/ndi/feature/ndibrowser/domain/

feature/theme-editor/
├── presentation/src/main/java/com/ndi/feature/themeeditor/
├── presentation/src/main/res/layout/fragment_theme_editor.xml
├── data/src/main/java/com/ndi/feature/themeeditor/data/
└── domain/src/main/java/com/ndi/feature/themeeditor/domain/

core/database/
└── src/main/java/com/ndi/core/database/

testing/e2e/
├── tests/
├── scripts/
└── package.json

scripts/
├── verify-android-prereqs.ps1
└── verify-e2e-dual-emulator-prereqs.ps1
```

**Structure Decision**: Use existing Android multi-module feature-first structure. Implement settings parity in feature presentation modules and nav wiring only, keep repositories/persistence in existing domain/data/core boundaries, and validate using existing `testing/e2e` and `scripts/*prereqs*` workflows.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | N/A | N/A |
