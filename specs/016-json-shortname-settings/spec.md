# Feature Specification: Theme Editor Settings

**Feature Branch**: `016-json-shortname-settings`  
**Created**: 2026-03-27  
**Status**: Draft  
**Input**: User description: "add a theme editor to the settings menu. I should be abel to select the accent color, and choose between dark mode, light mode or the system setting for dark/light mode (which already works)"

## Clarifications

### Session 2026-03-27

- Q: How should accent color options be offered? -> A: Provide a fixed curated palette of 6-8 accessible accent colors.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Choose App Theme Mode (Priority: P1)

As a user, I want to choose Light, Dark, or System theme mode from settings so the app matches my viewing preference.

**Why this priority**: Theme mode is the core control users expect when customizing app appearance and directly affects readability in every session.

**Independent Test**: Open Settings, change mode to Light, Dark, and System, and verify the app appearance updates accordingly.

**Acceptance Scenarios**:

1. **Given** the user opens the theme editor, **When** they choose Light mode, **Then** the app uses the light appearance immediately.
2. **Given** the user opens the theme editor, **When** they choose Dark mode, **Then** the app uses the dark appearance immediately.
3. **Given** the user opens the theme editor, **When** they choose System mode, **Then** the app follows the device dark/light setting.

---

### User Story 2 - Pick Accent Color (Priority: P1)

As a user, I want to choose an accent color so key highlights and action elements match my preferred visual style.

**Why this priority**: Accent color customization is the main value requested for the editor and improves personalization.

**Independent Test**: Open Settings, pick each available accent color option, and verify accent styling updates in visible UI elements.

**Acceptance Scenarios**:

1. **Given** the user opens the theme editor, **When** they select an accent color, **Then** accent-dependent UI elements update to that color.
2. **Given** multiple accent options are available, **When** the user switches between them, **Then** only one option is active at a time and the active choice is clearly indicated.
3. **Given** the theme editor is opened, **When** accent choices are displayed, **Then** the user can choose from a fixed curated palette of 6-8 accessible options.

---

### User Story 3 - Keep Theme Preferences (Priority: P2)

As a user, I want my selected mode and accent color to remain after leaving settings or relaunching the app so I do not need to reconfigure it.

**Why this priority**: Preference persistence prevents repeated setup and ensures a consistent experience.

**Independent Test**: Select theme mode and accent color, close and relaunch the app, then confirm the same selections remain applied.

**Acceptance Scenarios**:

1. **Given** the user has selected a theme mode and accent color, **When** they reopen settings later, **Then** the previous selections are shown as active.
2. **Given** the user has selected System mode, **When** device dark/light mode changes, **Then** the app appearance follows the new device mode without requiring another settings change.

### Visual Change Quality Gate *(mandatory when UI changes are present)*

- This feature changes visual behavior and MUST include Playwright end-to-end tests on emulator(s) for:
  - opening the theme editor from settings,
  - selecting Light, Dark, and System mode,
  - selecting available accent colors,
  - verifying selected options remain active after leaving and returning.
- This feature MUST run the existing Playwright e2e suite and keep all existing tests passing.

### Edge Cases

- User selects System mode while device is already in the matching appearance state: selection still saves and remains visible as active.
- App relaunches after a theme preference was saved on a previous version: saved values remain valid or gracefully fallback to default if unavailable.
- User rapidly switches accent options: only the final selection remains active and applied.
- Device theme changes while app is in foreground and System mode is active: app appearance updates without requiring restart.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a theme editor entry within the settings menu.
- **FR-002**: The theme editor MUST allow selecting exactly one theme mode from Light, Dark, and System.
- **FR-003**: When the user selects Light mode, the system MUST apply light appearance across the app.
- **FR-004**: When the user selects Dark mode, the system MUST apply dark appearance across the app.
- **FR-005**: When the user selects System mode, the system MUST follow the device dark/light setting.
- **FR-006**: The system MUST preserve current System mode behavior already supported by the app.
- **FR-007**: The theme editor MUST allow selecting one accent color from a fixed curated palette of 6-8 accessible accent options.
- **FR-008**: When the user selects an accent color, the system MUST apply that accent color to user-visible accent surfaces and controls.
- **FR-009**: The system MUST persist the selected theme mode and accent color across app restarts.
- **FR-010**: The system MUST show the currently active mode and accent color as selected when returning to theme settings.
- **FR-011**: For these visual behavior changes, the system MUST include emulator-run Playwright e2e coverage for all updated user-visible flows in this specification.
- **FR-012**: For these visual behavior changes, the system MUST execute and keep passing all existing Playwright e2e tests.

### Assumptions

- Accent color choices are drawn from a curated predefined set of 6-8 accessible options (not free-form color entry).
- Theme updates are expected to be visible without requiring users to manually clear app data.
- Existing settings navigation patterns and permissions remain unchanged.

### Key Entities *(include if feature involves data)*

- **ThemePreference**: Represents the user-selected appearance settings, including selected theme mode and accent color.
- **ThemeModeOption**: Represents one of the selectable mode values (Light, Dark, System), where exactly one is active.
- **AccentColorOption**: Represents one curated accessible accent choice in the fixed 6-8 option palette.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In acceptance testing, 100% of users can find and open the theme editor from settings in one navigation step.
- **SC-002**: In validation runs, 100% of Light, Dark, and System selections correctly apply their expected appearance behavior.
- **SC-003**: In validation runs, 100% of curated palette options (6-8) can be selected and visibly applied as the active accent.
- **SC-004**: In relaunch tests, 100% of selected theme mode and accent preferences remain applied after app restart.
- **SC-005**: In regression execution, all existing Playwright e2e tests remain passing and all newly added theme-editor visual-flow tests pass.
