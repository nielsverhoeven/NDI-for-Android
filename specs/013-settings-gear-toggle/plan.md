# Implementation Plan: Settings Gear Toggle

**Branch**: `013-settings-gear-toggle` | **Date**: 2026-03-26 | **Spec**: `specs/013-settings-gear-toggle/spec.md`
**Input**: Feature specification from `/specs/013-settings-gear-toggle/spec.md`

## Summary

Promote the existing settings action from an overflow-capable toolbar item to an
always-visible gear affordance on the source list, viewer, and output screens,
and add the same top-right gear affordance to the settings screen so the same
button closes the settings surface when it is already open. The implementation
will preserve the current single-activity Navigation Component architecture by
treating “open” as navigate to `settingsFragment` and “close” as pop back from
`settingsFragment`. Toggle intent and presentation state will be owned by the
relevant ViewModels, while fragments and screen classes stay limited to intent
dispatch and rendering. The work also adds deterministic JUnit and Playwright coverage for
visibility, toggle behavior, rapid taps, rotation/state restoration, and the
required existing-suite regression run.

## Technical Context

**Language/Version**: Kotlin 2.2.10 for Android modules, TypeScript for Playwright e2e, Gradle Kotlin DSL with Java toolchain 21 and Java 17 bytecode target  
**Primary Dependencies**: AndroidX Fragment/Lifecycle/ViewModel/Navigation, Kotlin Coroutines/Flow, Material 3 views, existing `NdiNavigation` helpers, Playwright dual-emulator harness in `testing/e2e`  
**Storage**: No new persistence; existing Room-backed settings storage remains unchanged and is reused by the existing settings feature  
**Testing**: JUnit4 unit tests, Android UI/navigation tests where already present, Playwright emulator e2e including the `@settings` suite plus existing regression suite  
**Target Platform**: Android API 24+ on the current single-activity Navigation Component architecture  
**Project Type**: Feature-modularized Android mobile application  
**Performance Goals**: Gear affordance visible immediately on render; settings open/close response under 1 second per spec; repeated taps must not leave duplicate settings destinations on stack  
**Constraints**: MVVM-only presentation logic; keep repository boundaries intact; no new permissions/background work; preserve existing settings fragment and deep link; retain Material 3 top app bar behavior; keep Playwright regression suite passing  
**Scale/Scope**: UI/menu and ViewModel updates across source list, viewer, output, and settings surfaces; small navigation helper updates; targeted unit, instrumentation, and Playwright coverage for one interaction flow

## Constitution Check (Pre-Design Gate)

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- MVVM-only presentation logic: PASS - settings toggle intent/state is owned by ViewModels and shared navigation helpers; fragments/screens remain thin event dispatchers and renderers.
- Single-activity navigation architecture: PASS - toggle behavior is implemented with existing nav graph destinations and back-stack operations.
- Repository-mediated data access: PASS - no new direct persistence/network access is introduced.
- Strict TDD (non-negotiable): PASS - plan requires failing JUnit/Playwright assertions first for visibility and toggle behavior.
- Unit test scope defined using JUnit: PASS - navigation/toggle helper and screen-level behavior will be covered with JUnit.
- Playwright e2e scope defined for end-to-end flows: PASS - `@settings` access-path tests are part of this feature.
- Emulator Playwright coverage for visual change: PASS - gear visibility/toggle path will be validated on emulator(s).
- Existing Playwright regression run explicitly planned: PASS - `npm run test:pr:primary` or equivalent regression suite run is mandatory evidence.
- Material 3 compliance verification planned: PASS - updated top app bars and gear affordances will include explicit Material 3 verification evidence in validation output.
- Battery/background execution impact evaluated: PASS - no new background work or long-running components.
- Offline-first and Room persistence constraints respected: PASS - feature does not alter persistence semantics.
- Least-permission/security implications documented: PASS - no new permissions or sensitive data exposure changes.
- Feature-module boundary compliance documented: PASS - changes stay in existing `app` and `feature/ndi-browser/presentation` boundaries.
- Release hardening validation planned: PASS - release hardening verification remains part of final validation.

## Project Structure

### Documentation (this feature)

```text
specs/013-settings-gear-toggle/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── settings-gear-toggle-ui-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
app/
├── src/main/java/com/ndi/app/navigation/NdiNavigation.kt
└── src/main/res/navigation/main_nav_graph.xml

feature/ndi-browser/presentation/
├── src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModel.kt
├── src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt
├── src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerViewModel.kt
├── src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerScreen.kt
├── src/main/java/com/ndi/feature/ndibrowser/output/OutputControlViewModel.kt
├── src/main/java/com/ndi/feature/ndibrowser/output/OutputControlFragment.kt
├── src/main/java/com/ndi/feature/ndibrowser/settings/SettingsViewModel.kt
├── src/main/java/com/ndi/feature/ndibrowser/settings/SettingsFragment.kt
├── src/main/res/layout/fragment_settings.xml
└── src/main/res/menu/
    ├── source_list_menu.xml
    ├── viewer_menu.xml
    └── output_menu.xml

testing/e2e/
├── README.md
├── tests/settings-navigation-source-list.spec.ts
├── tests/settings-navigation-viewer.spec.ts
├── tests/settings-navigation-output.spec.ts
└── tests/support/
    ├── android-ui-driver.ts
    └── e2e-suite-classification.spec.ts
```

**Structure Decision**: Reuse the existing feature-first Android modular
structure. Navigation helpers remain in `app`; menu/layout/fragment behavior
stays in `feature/ndi-browser/presentation`; existing settings persistence and
repositories are untouched; end-to-end validation remains in `testing/e2e`.

## Phase 0: Research Consolidation

Research output is captured in `specs/013-settings-gear-toggle/research.md` and
resolves technical decisions for:

- Reusing the existing `settingsFragment` instead of introducing a new bottom sheet.
- Making the gear action always visible with `showAsAction="always"` and a settings-screen top app bar action.
- Modeling “toggle” as nav open/pop-close with duplicate-navigation protection.
- Defining Playwright conversion work for the currently placeholder `@settings` access-path specs.
- Confirming scope boundaries for rotation, rapid taps, and regression evidence.

All technical context items are resolved; no `NEEDS CLARIFICATION` entries
remain.

## Phase 1: Design & Contracts

Design artifacts are complete and mapped to the specification:

- Data model: `specs/013-settings-gear-toggle/data-model.md`
  - Defines the UI interaction state, surface contract, and nav-stack toggle states.
- Contracts: `specs/013-settings-gear-toggle/contracts/settings-gear-toggle-ui-contract.md`
  - Defines gear visibility, open/close behavior, back-stack expectations, accessibility labels, and e2e observability.
- QuickStart: `specs/013-settings-gear-toggle/quickstart.md`
  - Documents test-first workflow, local validation commands, failing-test evidence capture, Material 3 verification, and required emulator regression evidence.
- Agent context update:
  - `.specify/scripts/bash/update-agent-context.sh copilot` executed after artifact generation.

## Constitution Check (Post-Design Re-Evaluation)

- MVVM gate: PASS - ViewModels own settings toggle/open-close intent and state decisions; screen classes and fragments only bind and render them.
- Navigation gate: PASS - toggle semantics are explicitly mapped to nav open/pop-close and remain single-activity compliant.
- Data gate: PASS - no direct UI-to-database access added.
- TDD gate: PASS - design requires failing JUnit and Playwright tests first, plus explicit failing-test evidence capture before implementation proceeds.
- UX gate: PASS - gear affordance uses Material top app bar actions, consistent placement, and explicit Material 3 verification evidence.
- Battery gate: PASS - no new background execution introduced.
- Offline gate: PASS - feature is navigation/UI-only and does not affect local-first behavior.
- Permission gate: PASS - no permission changes required.
- Modularity gate: PASS - all changes stay within existing module boundaries.
- Release gate: PASS - release hardening and Playwright regression validation remain required completion evidence.
- Platform gate: PASS - no toolchain or SDK changes are needed.

## Phase 2 Readiness

Planning artifacts ready for `/speckit.tasks`:

- `specs/013-settings-gear-toggle/spec.md`
- `specs/013-settings-gear-toggle/plan.md`
- `specs/013-settings-gear-toggle/research.md`
- `specs/013-settings-gear-toggle/data-model.md`
- `specs/013-settings-gear-toggle/contracts/settings-gear-toggle-ui-contract.md`
- `specs/013-settings-gear-toggle/quickstart.md`

## Complexity Tracking

No constitution violations require exception handling for this plan.
