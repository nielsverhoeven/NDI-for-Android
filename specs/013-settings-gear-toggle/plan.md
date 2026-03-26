# Implementation Plan: Settings Gear Toggle

**Branch**: `013-settings-gear-toggle` | **Date**: March 26, 2026 | **Spec**: `/specs/013-settings-gear-toggle/spec.md`
**Input**: Feature specification from `/specs/013-settings-gear-toggle/spec.md`

## Summary

Provide a consistent top-right settings affordance that is explicitly gear/cog iconography (never wrench/manage), stays visible on source list/viewer/output/settings surfaces, and toggles settings open/close with the same control.

Implementation remains within existing architecture: keep `settingsFragment` as the canonical settings destination, keep `NdiNavigation.settingsRequest()` for open behavior, and treat close behavior as a `popBackStack()` from the settings destination. Add/standardize top app bar behavior so settings shows a visible settings header/title and a persistent top-right gear icon while open.

## Technical Context

**Language/Version**: Kotlin 2.2.10, XML menu/layout resources, Gradle Kotlin DSL  
**Primary Dependencies**: AndroidX Fragment/Nav Component, Material Design 3 (`MaterialToolbar`/top app bar patterns), existing app navigation helpers (`NdiNavigation`)  
**Storage**: N/A (no new persistence; existing settings persistence unchanged)  
**Testing**: JUnit (unit), Android instrumentation where applicable, Playwright e2e on emulator(s)  
**Target Platform**: Android (minSdk 24, target/compile SDK 34 baseline per repository)  
**Project Type**: Mobile app (feature-first modularized Android project)  
**Performance Goals**: Settings toggle interaction completes with UI response under 1 second in emulator validation  
**Constraints**: Preserve existing module boundaries; enforce gear/cog iconography only (no wrench/manage iconography); keep top-right gear visible while settings is open; show visible settings header/title when settings is open; no new permissions/background work  
**Scale/Scope**: UI behavior on 4 in-scope surfaces (source list, viewer, output, settings) plus related navigation and e2e coverage

## UX Expectations

- Top-right settings affordance must use gear/cog iconography on all in-scope surfaces; wrench/manage iconography is out of contract.
- Gear affordance must remain visible and tappable while settings is open.
- Settings screen must present a visible header/title while open.
- Toggle semantics must remain consistent: gear opens settings when closed, and closes settings when open.

## Validation Scope

- Unit tests (JUnit) for toggle intent/state handling and navigation helper behavior.
- Emulator-run Playwright coverage for source list, viewer, and output settings toggle flows.
- Existing Playwright regression suite must run and remain passing in the same validation cycle.
- Material 3 verification for top app bar/title/action consistency on all in-scope surfaces.
- Release hardening verification remains required (`verifyReleaseHardening`).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] MVVM-only presentation logic enforced (toggle intent remains ViewModel/navigation-layer mediated; views stay thin)
- [x] Single-activity navigation compliance maintained (`settingsFragment` + nav graph route retained)
- [x] Repository-mediated data access preserved (no repository or persistence changes)
- [x] TDD evidence planned (failing tests first: unit + Playwright toggle flows)
- [x] Unit test scope defined using JUnit
- [x] Playwright e2e scope defined for end-to-end flows
- [x] For visual UI additions/changes: emulator Playwright e2e tests are explicitly planned
- [x] For visual UI additions/changes: existing Playwright e2e regression run is explicitly planned
- [x] Material 3 compliance verification planned for UI changes (top app bar/title/affordance consistency)
- [x] Battery/background execution impact evaluated (none; no new background execution)
- [x] Offline-first and Room persistence constraints respected (no persistence path changes)
- [x] Least-permission/security implications documented (no new permissions, exported components, or network/security surface)
- [x] Feature-module boundary compliance documented (presentation updates only; no boundary changes)
- [x] Release hardening validation planned (retain `verifyReleaseHardening` in validation scope)

Post-Phase-1 Re-check: PASS. Research and design artifacts keep all constitution gates satisfied with no justified violations.

## Project Structure

### Documentation (this feature)

```text
specs/013-settings-gear-toggle/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── README.md
│   └── settings-gear-toggle-ui-contract.md
└── tasks.md
```

### Source Code (repository root)
```text
app/
├── src/main/java/com/ndi/app/
│   ├── navigation/
│   └── di/
└── src/main/res/navigation/

feature/ndi-browser/
├── domain/src/main/java/com/ndi/feature/ndibrowser/domain/
├── data/src/main/java/com/ndi/feature/ndibrowser/data/
└── presentation/src/main/
    ├── java/com/ndi/feature/ndibrowser/
    │   ├── source_list/
    │   ├── viewer/
    │   ├── output/
    │   └── settings/
    └── res/
        ├── layout/
        └── menu/

testing/e2e/
└── tests/

core/
├── model/
└── database/
```

**Structure Decision**: Preserve the existing Android feature-first module graph. Limit changes to presentation/navigation resources and tests required for the gear-toggle UX contract. No new modules, no cross-boundary data/persistence changes, and no direct database access from UI layers.

## Complexity Tracking

No constitution violations require justification for this plan.
