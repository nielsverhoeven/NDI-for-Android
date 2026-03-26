# Feature Specification: Settings Gear Toggle

**Feature Branch**: `013-settings-gear-toggle`  
**Created**: March 23, 2026  
**Status**: Draft  
**Input**: User description: "the settings menu should be represented by a gear Icon. The gear icon should always be visible in the top right corner of the screen. When the settings menu is closed, the button should open the settings menu. If the settings menu is open, the button should close the settings menu."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Toggle Settings Menu (Priority: P1)

User sees a gear icon in the top right corner on the source list, viewer, output, and settings screens. The gear icon is always visible on those in-scope screens. When the user taps the gear icon, if the settings menu is closed, it opens the settings menu. If the settings menu is open, it closes the settings menu.

**Why this priority**: This is the core functionality for accessing the settings menu, enabling users to configure app preferences.

**Independent Test**: Can be fully tested by verifying the gear icon visibility and toggle behavior, delivering the ability to open/close settings independently.

**Acceptance Scenarios**:

1. **Given** the settings menu is closed, **When** the user taps the gear icon, **Then** the settings menu opens.
2. **Given** the settings menu is open, **When** the user taps the gear icon, **Then** the settings menu closes.

---

### Visual Change Quality Gate *(mandatory when UI changes are present)*

This feature adds visual behavior (gear icon and menu toggle), so the spec MUST define Playwright end-to-end tests that run on emulator(s) and cover the toggle flow. The spec MUST include a regression requirement to execute all existing Playwright e2e tests and keep them passing.

### Edge Cases

- What happens when the screen rotates? The gear icon should remain visible in the top right corner.
- How does the system handle rapid multiple taps on the gear icon? The toggle should work reliably without getting stuck in an inconsistent state.
- What if the settings menu is in a loading or error state? The toggle should still function to close the menu.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST display a gear icon in the top right corner of the source list, viewer, output, and settings screens whenever those screens are visible.
- **FR-002**: System MUST open the settings menu when the gear icon is tapped and the settings menu is currently closed.
- **FR-003**: System MUST close the settings menu when the gear icon is tapped and the settings menu is currently open.
- **FR-004**: For visual additions/changes, system MUST include emulator-run Playwright e2e coverage for the gear icon toggle functionality.
- **FR-005**: For visual additions/changes, system MUST execute and keep passing all existing Playwright e2e tests.

### Key Entities *(include if feature involves data)*

No key entities involved, as this is a UI interaction feature without data persistence requirements.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can open the settings menu by tapping the gear icon with a response time under 1 second.
- **SC-002**: The gear icon remains visible in the top right corner across the source list, viewer, output, and settings screens in both portrait and landscape orientations.
- **SC-003**: Settings menu toggle functionality is considered reliable when all feature acceptance tests (JUnit, instrumentation, and Playwright toggle coverage) pass in one clean validation run with no skipped tests.
- **SC-004**: No regression in existing UI functionality, with all existing e2e tests passing.

## Assumptions

- The settings menu component already exists and can be programmatically opened and closed.
- The source list, viewer, output, and settings screens have a consistent top-app-bar structure where the gear icon can be integrated without expanding scope to unrelated screens.
