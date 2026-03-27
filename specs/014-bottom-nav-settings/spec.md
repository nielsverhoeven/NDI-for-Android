# Feature Specification: Bottom Navigation Settings Access

**Feature Branch**: 014-bottom-nav-settings  
**Created**: March 26, 2026  
**Status**: Draft  
**Input**: User description: replace top-right affordance requirements with bottom-navigation requirements.

## Clarifications

### Session 2026-03-26

- Q: Which bottom navigation model should settings use? -> A: Add a dedicated Settings bottom navigation item alongside Home, Stream, and View.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Open Settings From Bottom Navigation (Priority: P1)

As a user, I can open settings from a dedicated bottom navigation item instead of a top-right toolbar icon, so settings access is consistent with the main app navigation model.

**Why this priority**: Accessing settings is a core task and the entry point change is the primary feature outcome.

**Independent Test**: From Home, Stream, and View, select the Settings bottom navigation item and verify navigation to settings with correct selected-state highlighting.

**Acceptance Scenarios**:

1. **Given** Home is visible, **When** the user selects Settings in bottom navigation, **Then** the settings screen opens and Settings is the selected bottom navigation item.
2. **Given** Stream or View is visible, **When** the user selects Settings in bottom navigation, **Then** the settings screen opens and Settings is the selected bottom navigation item.

---

### User Story 2 - Leave Settings Through Bottom Navigation (Priority: P2)

As a user, I can leave settings by selecting Home, Stream, or View from bottom navigation.

**Why this priority**: It preserves predictable navigation after replacing the top-right toggle model.

**Independent Test**: Open settings, then select each non-settings bottom navigation item and verify the app navigates to the selected destination with matching selected state.

**Acceptance Scenarios**:

1. **Given** settings is visible, **When** the user selects Home, **Then** Home opens and Home is selected.
2. **Given** settings is visible, **When** the user selects Stream or View, **Then** the corresponding destination opens and selected state matches.

---

### User Story 3 - Remove Top-Right Settings Entry Affordance (Priority: P3)

As a user, I no longer rely on a top-right settings entry action on source list, viewer, output, or settings surfaces.

**Why this priority**: It avoids duplicate entry paths and conflicting navigation metaphors.

**Independent Test**: Verify in-scope screens do not expose a top-right settings entry affordance while settings access remains available via bottom navigation.

**Acceptance Scenarios**:

1. **Given** source list, viewer, output, or settings is visible, **When** the screen renders, **Then** no top-right settings entry action is shown.

---

### Visual Change Quality Gate *(mandatory when UI changes are present)*

- This feature changes visible navigation behavior and MUST include emulator-run Playwright coverage for bottom-navigation settings entry and exit flows.
- This feature changes visible navigation behavior and MUST execute the existing Playwright regression suite with passing results.

### Edge Cases

- If Settings is selected while already on settings, the app remains stable and does not create duplicate settings destinations.
- If the user switches tabs rapidly between Settings and other bottom navigation items, selected state and visible destination remain synchronized.
- If device rotation occurs while on settings or during bottom navigation switching, destination and selected state remain consistent.
- If a deep link opens viewer or output first, settings remains reachable through bottom navigation without requiring a top-right action.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a dedicated Settings item in bottom navigation.
- **FR-002**: System MUST navigate to the settings screen when the Settings bottom navigation item is selected.
- **FR-003**: System MUST navigate away from settings when Home, Stream, or View is selected from bottom navigation.
- **FR-004**: System MUST keep bottom navigation selected state synchronized with the currently visible destination, including settings.
- **FR-005**: System MUST remove settings entry from top-right toolbar actions on source list, viewer, output, and settings surfaces.
- **FR-006**: System MUST preserve a visible settings header/title while the settings screen is active.
- **FR-007**: For visual changes, system MUST include emulator-run Playwright e2e coverage for bottom-navigation settings entry and exit behavior.
- **FR-008**: For visual changes, system MUST execute and keep passing all existing Playwright e2e tests.

### Key Entities *(include if feature involves data)*

- **Navigation Destination State**: Represents current destination and selected bottom navigation item.
- **Settings Entry Affordance**: Represents the canonical settings entry control, constrained to bottom navigation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can reach settings from Home, Stream, and View by selecting the bottom navigation Settings item in one interaction.
- **SC-002**: Validation confirms no in-scope screen displays a top-right settings entry affordance.
- **SC-003**: Bottom-navigation settings entry and exit e2e tests pass in a clean run with no skipped tests.
- **SC-004**: Existing Playwright regression suite passes with no new failures attributable to this feature.
- **SC-005**: Selected bottom navigation state matches the active destination in rotation and rapid tab-switch scenarios.

## Assumptions

- The app remains single-activity with Navigation Component and bottom navigation at top-level scope.
- Settings remains a destination screen, not a modal surface.
- Replacing the top-right settings entry path with bottom navigation is acceptable for discoverability.
