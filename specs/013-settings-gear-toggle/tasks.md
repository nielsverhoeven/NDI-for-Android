# Tasks: Settings Gear Toggle

**Input**: Design documents from `/specs/013-settings-gear-toggle/`
**Prerequisites**: `plan.md` (required), `spec.md` (required), `research.md`, `data-model.md`, `contracts/settings-gear-toggle-ui-contract.md`, `quickstart.md`

**Tests**: Tests are required for this feature per constitution TDD gate and plan requirements. Every story task sequence must write failing tests first, record red-phase evidence, include emulator-run Playwright coverage for the visual toggle flow, and record a passing existing-suite regression run.

**Organization**: Tasks are grouped by user story so the gear toggle can be implemented and validated independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: User story label (`US1`) for story-phase tasks only
- Every task includes concrete file path(s)

## Phase 1: Setup (Shared UI Scaffolding)

**Purpose**: Create the shared UI resources and validation target used by the toggle implementation.

- [X] T001 Create settings-screen top app bar menu resource in `feature/ndi-browser/presentation/src/main/res/menu/settings_menu.xml` with a top-right gear/cog settings action (no wrench/manage variant)
- [X] T002 [P] Add gear-toggle accessibility labels and any required top-app-bar strings in `feature/ndi-browser/presentation/src/main/res/values/strings.xml`
- [X] T003 [P] Restructure the settings screen layout to host a top app bar above the existing form in `feature/ndi-browser/presentation/src/main/res/layout/fragment_settings.xml`, including a visible settings header/title container
- [X] T004 [P] Create validation evidence stub for this feature, including red-phase, Material 3, and regression sections, in `test-results/settings-gear-toggle-validation.md`

---

## Phase 2: Foundational (Shared Toggle Infrastructure)

**Purpose**: Establish the shared navigation and automation infrastructure that blocks story implementation.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T005 Add canonical settings-toggle navigation helpers and ViewModel-facing toggle contracts in `app/src/main/java/com/ndi/app/navigation/NdiNavigation.kt`
- [X] T006 [P] Extend settings navigation unit-test coverage for open/close, duplicate-guard, and ViewModel-facing helper behavior in `app/src/test/java/com/ndi/app/navigation/NdiNavigationSettingsTest.kt`
- [X] T007 [P] Extend Android UI driver selectors for always-visible settings affordances and close-on-settings behavior in `testing/e2e/tests/support/android-ui-driver.ts`
- [X] T008 Update settings suite classification and validation references for toggle coverage in `testing/e2e/tests/support/e2e-suite-classification.spec.ts`

**Checkpoint**: Foundation complete. User Story 1 can now be implemented and validated.

---

## Phase 3: User Story 1 - Toggle Settings Menu (Priority: P1) 🎯 MVP

**Goal**: Users can always see a gear icon in the top-right corner, tap it to open settings from source list/viewer/output, and tap the same gear on the settings screen to close settings and return to the previous surface.

**Independent Test**: From source list, viewer, and output, verify a top-right gear/cog (not wrench/manage) is visible without overflow, opens settings on tap, and from settings the same top-right gear remains visible with a visible settings header/title and closes back to the originating surface while remaining stable across rapid taps and rotation.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation.**

- [X] T009 [P] [US1] Add source-list Playwright coverage for always-visible top-right gear/cog iconography and open-settings toggle behavior in `testing/e2e/tests/settings-navigation-source-list.spec.ts`
- [X] T010 [P] [US1] Add viewer Playwright coverage for always-visible top-right gear/cog iconography and open-settings toggle behavior in `testing/e2e/tests/settings-navigation-viewer.spec.ts`
- [X] T011 [P] [US1] Add output Playwright coverage for always-visible top-right gear/cog iconography and open-settings toggle behavior in `testing/e2e/tests/settings-navigation-output.spec.ts`
- [X] T012 [P] [US1] Add instrumentation coverage for settings-screen visible header/title, persistent top-right gear while open, close-on-gear behavior, rapid taps, and rotation resilience in `feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/settings/SettingsGearToggleTest.kt`
- [X] T013 [US1] Record failing-test evidence for JUnit, instrumentation, and Playwright red-phase runs in `test-results/settings-gear-toggle-validation.md`

### Implementation for User Story 1

- [X] T014 [P] [US1] Make the source-list settings action always visible as gear/cog iconography (no wrench/manage icon) in `feature/ndi-browser/presentation/src/main/res/menu/source_list_menu.xml`
- [X] T015 [P] [US1] Make the viewer settings action always visible as gear/cog iconography (no wrench/manage icon) in `feature/ndi-browser/presentation/src/main/res/menu/viewer_menu.xml`
- [X] T016 [P] [US1] Make the output settings action always visible as gear/cog iconography (no wrench/manage icon) in `feature/ndi-browser/presentation/src/main/res/menu/output_menu.xml`
- [X] T017 [US1] Implement settings-screen top app bar render/bind wiring with a visible settings header/title and persistent top-right gear action in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsFragment.kt` and `feature/ndi-browser/presentation/src/main/res/menu/settings_menu.xml`
- [X] T018 [US1] Add source-list settings toggle intent/state handling in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModel.kt` and bind it in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt`
- [X] T019 [US1] Add viewer settings toggle intent/state handling in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerViewModel.kt` and bind it in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerScreen.kt`
- [X] T020 [US1] Add output settings toggle intent/state handling in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlViewModel.kt` and bind it in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlFragment.kt`
- [X] T021 [US1] Add settings-screen close-on-gear intent/state handling in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsViewModel.kt`, bind it in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsFragment.kt`, and ensure nav restoration safety in `app/src/main/res/navigation/main_nav_graph.xml` so the settings gear remains visible while open
- [X] T022 [US1] Update accessibility/discoverability semantics for Playwright and TalkBack in `feature/ndi-browser/presentation/src/main/res/values/strings.xml` and `feature/ndi-browser/presentation/src/main/res/layout/fragment_settings.xml`, including explicit settings header/title labeling

**Checkpoint**: User Story 1 is independently functional and ready for demonstration.

---

## Phase 4: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, release hardening, and documentation/evidence capture.

- [ ] T023 [P] Update feature-specific validation notes and local run guidance in `specs/013-settings-gear-toggle/quickstart.md` to explicitly call out gear/cog-only iconography, persistent top-right gear visibility while settings is open, and visible settings header/title checks
- [ ] T024 Run JUnit and instrumentation validation for toggle flow plus settings header/title and persistent top-right gear assertions, then record results in `test-results/settings-gear-toggle-validation.md`
- [ ] T025 Run emulator Playwright toggle coverage and existing-suite regression validation with explicit checks for gear/cog-only iconography, persistent top-right gear while open, and visible settings header/title, then record results in `test-results/settings-gear-toggle-validation.md`
- [ ] T026 Run release-hardening verification for the updated APK and capture results in `test-results/settings-gear-toggle-validation.md`
- [ ] T027 Run Material 3 compliance verification for the updated top app bars, including top-right gear persistence and visible settings title treatment, and record results in `test-results/settings-gear-toggle-validation.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies, start immediately.
- **Phase 2 (Foundational)**: Depends on Phase 1 completion; blocks all story work.
- **Phase 3 (US1)**: Depends on Phase 2 completion.
- **Phase 4 (Polish)**: Depends on US1 completion.

### User Story Dependencies

- **US1 (P1)**: Independent after foundational work; no dependency on other user stories.

### Within User Story 1

- Write and run failing tests first (`T009`-`T013`), covering gear/cog-only iconography and settings-header/persistent-gear assertions.
- Promote always-visible gear/cog menu resources before wiring screen behavior (`T014`-`T017`).
- Apply shared navigation/toggle routing through ViewModels on source list, viewer, and output after the helper is in place (`T018`-`T020`).
- Finish with settings close behavior plus settings-open persistence/accessibility adjustments (`T021`, `T022`).

### Parallel Opportunities

- Setup tasks marked `[P]` (`T002`, `T003`, `T004`) can run concurrently.
- Foundational tasks marked `[P]` (`T006`, `T007`) can run concurrently after `T005` starts.
- US1 Playwright tests (`T009`, `T010`, `T011`) can run in parallel.
- Always-visible menu updates (`T014`, `T015`, `T016`) can run in parallel.
- Final validation tasks `T024`, `T025`, and `T027` can be executed in parallel when infrastructure allows.

---

## Parallel Example: User Story 1

```bash
# Launch the three Playwright entry-path tests together:
Task: "T009 [US1] source-list gear/cog visibility and toggle Playwright coverage"
Task: "T010 [US1] viewer gear/cog visibility and toggle Playwright coverage"
Task: "T011 [US1] output gear/cog visibility and toggle Playwright coverage"

# Launch the three always-visible menu updates together:
Task: "T014 [US1] source-list always-visible gear/cog menu update"
Task: "T015 [US1] viewer always-visible gear/cog menu update"
Task: "T016 [US1] output always-visible gear/cog menu update"
```

---

## Implementation Strategy

### MVP First (US1 Only)

1. Complete Phase 1 and Phase 2.
2. Deliver Phase 3 so the gear toggle works across source list, viewer, output, and settings.
3. Validate the full toggle flow independently before moving to polish.

### Incremental Delivery

1. Setup and shared navigation infrastructure complete.
2. Add failing Playwright and instrumentation coverage.
3. Implement always-visible gear/cog affordances, visible settings header/title, and close-on-settings behavior through ViewModels plus thin screen bindings.
4. Run final validation, Material 3 verification, and release-hardening evidence capture.

### Parallel Team Strategy

1. One developer handles shared navigation/test-harness infrastructure (`T005`-`T008`).
2. One developer handles Playwright/instrumentation coverage (`T009`-`T013`).
3. One developer handles menu/layout/screen integration (`T014`-`T022`).
4. Merge into final validation and evidence capture (`T023`-`T027`).

---

## Notes

- All tasks follow the constitution TDD requirement: tests must fail before implementation begins.
- This feature reuses the existing settings fragment; do not replace it with a dialog or bottom sheet.
- Existing Playwright regression coverage is a release gate for this visual change and must be recorded explicitly.
