# Implementation Plan: Fix Appearance Settings

**Branch**: `025-fix-appearance-settings` | **Date**: 2026-03-31 | **Spec**: `specs/025-fix-appearance-settings/spec.md`
**Input**: Feature specification from `/specs/025-fix-appearance-settings/spec.md`

## Summary

Fix Appearance regressions by reconnecting theme mode persistence to app-wide runtime theme application, restoring the color theme entry point in Settings > Appearance (compact and wide layouts), and adding Playwright coverage that validates Light/Dark/System behavior using hybrid assertions plus full regression execution.

## Technical Context

**Language/Version**: Kotlin 2.2.10, TypeScript (Playwright), PowerShell 7 scripts  
**Primary Dependencies**: AndroidX Fragment/ViewModel/Lifecycle, Material3, Navigation Component, Kotlin Coroutines/Flow, Room, Playwright test runner  
**Storage**: Room `settings_preference` single-row table via `SettingsPreferenceDao`  
**Testing**: JUnit unit tests, Android instrumentation where applicable, Playwright e2e in `testing/e2e`  
**Target Platform**: Android API 24+ (compile/target 34 baseline), emulator-driven e2e validation  
**Project Type**: Multi-module Android mobile app  
**Performance Goals**: Theme mode apply time <= 1 second after save; no added startup delay beyond existing baseline  
**Constraints**: Preserve MVVM and repository boundaries, no new permissions/background work, maintain existing deep-link routes, keep e2e deterministic with preflight gates  
**Scale/Scope**: One feature slice across `app`, `feature/ndi-browser`, `feature/theme-editor`, and `testing/e2e`; appearance-specific tests plus full Playwright regression

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

### Post-Design Constitution Re-check

- [x] Phase 0 decisions preserve repository-mediated data flow and avoid UI coupling.
- [x] Phase 1 artifacts include explicit e2e + regression + preflight requirements.
- [x] No constitution violations identified; Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/025-fix-appearance-settings/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── appearance-settings-ui-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
app/
└── src/main/java/com/ndi/app/theme/
    └── AppThemeCoordinator.kt

feature/ndi-browser/
├── presentation/src/main/java/com/ndi/feature/ndibrowser/settings/
│   ├── SettingsFragment.kt
│   ├── SettingsDetailRenderer.kt
│   └── SettingsViewModel.kt
└── data/src/main/java/com/ndi/feature/ndibrowser/data/repository/
    └── NdiSettingsRepositoryImpl.kt

feature/theme-editor/
├── domain/src/main/java/com/ndi/feature/themeeditor/domain/repository/
│   └── ThemeEditorRepository.kt
└── data/src/main/java/com/ndi/feature/themeeditor/data/repository/
    └── ThemeEditorRepositoryImpl.kt

core/
├── model/src/main/java/com/ndi/core/model/NdiSettingsModels.kt
└── database/src/main/java/com/ndi/core/database/NdiDatabase.kt

testing/e2e/
├── tests/
│   ├── 024-settings-menu-rebuild.spec.ts
│   └── support/*.spec.ts
└── scripts/run-primary-pr-e2e.ps1
```

**Structure Decision**: Keep the existing Android feature-first modular structure. Implement appearance fixes within existing presentation/repository modules and add e2e coverage in the current Playwright suite, avoiding any new modules or architectural reshaping.

## Complexity Tracking

No constitution violations requiring justification.
