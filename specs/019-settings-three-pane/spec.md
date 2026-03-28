# Feature Specification: Three-Column Settings Layout

**Feature Branch**: `019-settings-three-pane`  
**Created**: 2026-03-28  
**Status**: Draft  
**Input**: User description: "I want to change the way the settings area of the app works. When the app is used on a tablet, or on a phone in landscape mode, the screen should be split in 3 columns; 1. main navigation (home, stream, view, settings) 2. settings menu 3. adjustable settings for the selected settings menu item"

## Clarifications

### Session 2026-03-28

- Q: Which rule should trigger three-column settings mode? -> A: Use existing app wide-layout criteria for both tablets and landscape phones.

## User Scenarios & Testing *(mandatory)*

<!--
  IMPORTANT: User stories should be PRIORITIZED as user journeys ordered by importance.
  Each user story/journey must be INDEPENDENTLY TESTABLE - meaning if you implement just ONE of them,
  you should still have a viable MVP (Minimum Viable Product) that delivers value.
  
  Assign priorities (P1, P2, P3, etc.) to each story, where P1 is the most critical.
  Think of each story as a standalone slice of functionality that can be:
  - Developed independently
  - Tested independently
  - Deployed independently
  - Demonstrated to users independently
-->

### User Story 1 - Browse and configure settings in one workspace (Priority: P1)

As a user on a tablet or a phone in landscape, I can stay on a single settings workspace that shows navigation, settings categories, and detailed controls at the same time.

**Why this priority**: This is the core behavior change and delivers the primary value of faster, less disruptive settings management.

**Independent Test**: Open settings on a tablet or landscape phone, select multiple settings categories, and confirm the three columns remain visible while detail controls update in place.

**Acceptance Scenarios**:

1. **Given** the app is in settings and the current configuration meets the app's existing wide-layout criteria, **When** the screen loads, **Then** the layout shows three simultaneous columns: main navigation, settings menu, and selected settings details.
2. **Given** the three-column settings layout is visible, **When** the user selects a different settings menu item, **Then** only the details column content changes and the first two columns remain visible.

---

### User Story 2 - Keep context while navigating app sections (Priority: P2)

As a user adjusting preferences, I can switch between main navigation destinations directly from the first column without losing orientation in the app.

**Why this priority**: Persistent main navigation in the layout reduces back-and-forth navigation and improves usability for larger/wider screens.

**Independent Test**: From the settings three-column layout, use main navigation items and verify destinations open correctly with expected active state indication.

**Acceptance Scenarios**:

1. **Given** the three-column layout is displayed, **When** the user selects a main navigation item, **Then** the app navigates to that destination and keeps navigation state clear.
2. **Given** the user returns to settings in supported orientation/device class, **When** settings opens again, **Then** the three-column layout is shown.

---

### User Story 3 - Fall back gracefully on unsupported layouts (Priority: P3)

As a user on a phone in portrait orientation, I continue to get a usable settings experience without broken or cramped panels.

**Why this priority**: Protects compatibility and avoids regressions for the most constrained layout.

**Independent Test**: Open settings on phone portrait and verify the app uses a non-three-column experience that remains functional for selecting categories and changing settings.

**Acceptance Scenarios**:

1. **Given** the app is on a phone in portrait orientation, **When** settings is opened, **Then** the app uses the compact settings presentation instead of forcing a three-column layout.
2. **Given** the user rotates from landscape/tablet layout to portrait while in settings, **When** orientation changes, **Then** the settings experience remains functional and preserves in-progress context for the selected category when possible.

---

### Visual Change Quality Gate *(mandatory when UI changes are present)*

- This feature changes visual behavior. Validation MUST include emulator-run Playwright end-to-end coverage for:
  - entering settings on a tablet/wide layout and confirming three columns are visible,
  - changing a settings menu selection and confirming detail content updates in the third column,
  - rotating or using portrait layout and confirming compact fallback behavior.
- Regression validation MUST execute the full existing Playwright e2e suite and keep all tests passing.

### Test Environment & Preconditions *(mandatory)*

- Required runtime dependencies:
  - Android emulator or device representing tablet or wide-screen profile.
  - Android emulator or device representing phone portrait profile.
  - Existing e2e tooling prerequisites already required by the repository (SDK, emulator tooling, and test harness readiness).
- Preflight command/check:
  - Run `scripts/verify-android-prereqs.ps1` before executing feature e2e validation.
  - Record preflight result (pass/fail) in validation artifacts before running visual flows.
- Blocked execution handling:
  - If required emulator/device profiles are unavailable or preflight fails, mark the validation result as `BLOCKED-ENV`.
  - Include the failed preflight output and concrete unblocking action (for example: missing SDK component or unavailable emulator image).

### Edge Cases

- User opens settings in supported wide layout but no specific settings category is preselected.
- User rotates between supported (wide) and unsupported (compact) layouts while editing settings.
- A settings category has no adjustable options available.
- Text scaling or accessibility font size increases and risks column crowding.
- Very long settings category names or localized strings exceed expected widths.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The settings area MUST render as a three-column layout whenever the current configuration matches the app's existing wide-layout criteria (including qualified tablet and landscape phone configurations).
- **FR-002**: The first column MUST expose the app's main navigation entries: Home, Stream, View, and Settings.
- **FR-003**: The second column MUST present a settings menu with selectable settings categories.
- **FR-004**: The third column MUST show adjustable settings controls for the currently selected settings menu category.
- **FR-005**: Selecting a different settings menu category MUST update only the third column content without replacing the full settings workspace.
- **FR-006**: The layout MUST clearly indicate which settings menu category is currently selected.
- **FR-007**: On phone portrait orientation, the app MUST use a compact settings presentation rather than forcing the three-column layout.
- **FR-008**: When switching between supported and compact layouts (for example due to rotation), the system MUST preserve the user's current settings category context when possible.
- **FR-009**: If a selected settings category has no adjustable options, the details column MUST display a clear empty-state message.
- **FR-010**: For visual additions/changes, the system MUST include emulator-run Playwright e2e coverage for all new/updated settings flows.
- **FR-011**: For visual additions/changes, the system MUST execute and keep passing all existing Playwright e2e tests.
- **FR-012**: For environment-dependent validations, the system MUST run and record preflight checks before executing end-to-end gates.
- **FR-013**: Validation reporting MUST classify each failed/blocked gate as code failure or environment blocker with reproduction details.

### Key Entities *(include if feature involves data)*

- **Layout Context**: Represents the active layout mode for settings (`three-column` or `compact`) based on device type/orientation.
- **Main Navigation Item**: Represents one primary destination entry (Home, Stream, View, Settings) with active/inactive state.
- **Settings Category**: Represents a selectable settings menu item shown in column two.
- **Settings Detail Group**: Represents the adjustable settings controls associated with a selected settings category.

### Assumptions

- Existing settings categories and adjustable controls remain functionally unchanged; this feature changes presentation and interaction flow.
- "Tablet" and "phone in landscape" eligibility for three-column mode is determined exclusively by the app's existing wide-layout criteria.
- Compact behavior for portrait phones continues current expected settings behavior.
- This feature does not introduce new permissions, account roles, or backend dependencies.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In usability validation for supported wide layouts, at least 90% of participants can navigate to a target settings category and modify one setting in under 30 seconds.
- **SC-002**: In feature e2e validation runs, 100% of required three-column settings acceptance scenarios pass on at least one tablet/wide profile and one compact phone profile.
- **SC-003**: At least 95% of layout transitions between wide and compact modes preserve the currently selected settings category context during test runs.
- **SC-004**: Post-release feedback related to "difficult settings navigation on large screens" decreases by at least 30% within one release cycle compared with the prior cycle baseline.
