# Feature Specification: Fix Appearance Settings

**Feature Branch**: `025-fix-appearance-settings`
**Created**: 2026-03-31
**Status**: Draft
**Input**: User description: "I want to do some adjustments to the appearance settings menu because it is not working correctly: The light, dark and system default mode are not working. Fix that. The option to change the color theme of the app is gone. I want it back. Add e2e tests that validate changing these appearance settings are working."

## Clarifications

### Session 2026-03-31

- Q: How should e2e validate applied theme mode to minimize flakiness while still proving behavior? -> A: Use hybrid validation: verify persisted selection plus one stable visual rendering assertion.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Fix Theme Mode Switching (Priority: P1)

A user opens the Settings screen and navigates to the Appearance category. They select a theme mode (Light, Dark, or System Default) from the radio buttons and save. The app immediately switches to the chosen mode and remains in that mode after relaunching.

**Why this priority**: Theme mode switching is a core accessibility and comfort feature. Currently none of the three modes work, making the Appearance settings category non-functional for users.

**Independent Test**: Open Settings > Appearance, select each mode, tap Save, and verify the app visual theme changes and remains correct after restart.

**Acceptance Scenarios**:

1. **Given** the app is running with any theme mode, **When** the user opens Settings > Appearance and selects "Light", then taps Save, **Then** the app immediately switches to light mode.
2. **Given** the app is in light mode, **When** the user selects "Dark" and taps Save, **Then** the app immediately switches to dark mode.
3. **Given** the app is in light or dark mode, **When** the user selects "System Default" and taps Save, **Then** the app follows the device-level theme setting.
4. **Given** the user saved a theme mode preference, **When** the app is restarted, **Then** the same mode is restored.

---

### User Story 2 - Restore Color Theme Picker (Priority: P2)

A user opens Settings > Appearance. They see both theme mode controls and a color theme entry point. They can open the Theme Editor, choose an accent, and the app accent updates.

**Why this priority**: The color theme option regressed and is missing. Restoring it is required for personalization and parity with existing app capabilities.

**Independent Test**: Open Settings > Appearance, verify color theme control is visible and tappable, navigate to Theme Editor, select a new accent, and verify accent change.

**Acceptance Scenarios**:

1. **Given** the user is in Settings > Appearance, **When** the panel renders, **Then** a color theme entry point is visible.
2. **Given** the color theme entry point is visible, **When** the user taps it, **Then** the Theme Editor screen opens.
3. **Given** Theme Editor is open, **When** the user selects a different accent color, **Then** app accent styling updates.
4. **Given** an accent color was previously selected, **When** the user returns to Settings > Appearance, **Then** the current color theme state is represented.

---

### User Story 3 - E2E Coverage for Appearance (Priority: P3)

Automated end-to-end tests validate Light, Dark, and System Default mode behavior and verify the restored color theme entry point.

**Why this priority**: The tests prevent regression of the broken appearance functionality and establish release confidence.

**Independent Test**: Run the appearance e2e suite on emulator; verify all scenarios pass without relying on internal implementation details.

**Acceptance Scenarios**:

1. **Given** an emulator with the app installed, **When** e2e selects Light mode and saves, **Then** persisted selection and a stable light-mode visual token are both verified.
2. **Given** an emulator with the app installed, **When** e2e selects Dark mode and saves, **Then** persisted selection and a stable dark-mode visual token are both verified.
3. **Given** an emulator with the app installed, **When** e2e selects System Default and saves, **Then** persisted selection is verified and the app follows device theme change.
4. **Given** Settings > Appearance is opened, **When** the panel renders, **Then** the color theme entry point is visible and navigable.
5. **Given** existing Playwright e2e tests, **When** full regression runs, **Then** all previously passing tests stay passing.

---

### Visual Change Quality Gate *(mandatory when UI changes are present)*

This feature changes user-visible behavior in Settings > Appearance. Playwright e2e tests on emulator MUST cover:

- Light, Dark, and System Default selection and save behavior.
- Hybrid theme verification for Light and Dark: persisted selection plus one stable visual token.
- System Default follow-system verification by changing device theme during test.
- Color theme entry point visibility and navigation.
- Full existing Playwright regression with no newly introduced failures.

### Test Environment & Preconditions *(mandatory)*

- **Runtime dependencies**: Android emulator (API 34, x86_64 recommended), app installed, ADB accessible.
- **Preflight check**: Run `scripts/verify-android-prereqs.ps1` and ensure exit code 0 before e2e execution.
- **Blocked handling**: If emulator or ADB is unavailable, mark result BLOCKED and record emulator serial, `adb devices` output, and required unblocking action.

### Edge Cases

- Unsaved theme mode change is discarded when user leaves Settings without Save.
- App restart immediately after Save restores saved mode.
- System Default mode reacts to device theme change while app remains running.
- Backing out of Theme Editor without changes preserves prior accent.
- Missing navigation target for Theme Editor must not crash the app.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Selecting Light and saving MUST switch the app to light mode immediately.
- **FR-002**: Selecting Dark and saving MUST switch the app to dark mode immediately.
- **FR-003**: Selecting System Default and saving MUST make the app follow device theme.
- **FR-004**: Selected theme mode MUST persist across app restart.
- **FR-005**: Reopening Settings > Appearance MUST show current saved theme mode as selected.
- **FR-006**: Appearance panel MUST include a color theme entry point in compact and wide layouts.
- **FR-007**: Tapping the color theme entry point MUST navigate to existing Theme Editor.
- **FR-008**: Theme Editor accent selection MUST update app accent styling and persist.
- **FR-009**: Appearance e2e MUST validate Light and Dark using hybrid verification: persisted selection plus a stable visual token assertion where token = Settings top app bar surface luminance bucket (light-mode bucket vs dark-mode bucket) using a deterministic helper.
- **FR-010**: Appearance e2e MUST validate System Default by toggling device theme and confirming app follows.
- **FR-011**: Existing Playwright e2e suite MUST remain passing after the feature changes.
- **FR-012**: Preflight environment checks MUST be run and recorded before e2e validation.
- **FR-013**: Validation results MUST classify failures as code failure or environment blocker with reproduction details.
- **FR-014**: Validation MUST include automated measurement that confirms theme-mode apply latency from Save action to applied runtime mode is <= 1 second on supported test devices.

### Key Entities

- **Theme Mode**: User-selected app brightness mode: Light, Dark, or System Default.
- **Color Theme (Accent)**: User-selected accent palette managed via Theme Editor.
- **Appearance Settings Panel**: Settings detail panel for theme mode and color theme controls.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Saving a mode selection applies the mode within 1 second on supported test devices.
- **SC-002**: Color theme entry point is visible in Appearance panel in 100% of compact and wide layout test runs.
- **SC-003**: E2E passes for all three modes with 0 failures using hybrid validation for Light/Dark and follow-system validation for System Default.
- **SC-004**: Full existing Playwright regression has 0 newly introduced failures.
- **SC-005**: Restart-after-save restores selected mode in 100% of executed runs.

## Assumptions

- Existing `ndi://theme-editor` route remains valid.
- Save action is explicit; unsaved changes are not applied.
- NDI runtime dependencies are not required for appearance-only validation.
