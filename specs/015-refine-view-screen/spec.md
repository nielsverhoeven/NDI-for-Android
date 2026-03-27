# Feature Specification: Refine View Screen Controls

**Feature Branch**: `015-refine-view-screen`  
**Created**: 2026-03-27  
**Status**: Draft  
**Input**: User description: "I want the following adjustments on the view screen: Do not display the current device as an option. There should be no button that allows for starting an output; that should only be part of the stream menu. There should be a button stating 'view stream' for each available source. Only the button should be clickable. The loading icon should be next to the refresh button. While refreshing, the refresh button should be disabled. The refresh button should be in the bottom left corner."

## Clarifications

### Session 2026-03-27

- Q: During refresh, should existing source list remain visible or clear immediately? → A: Keep the existing list visible while refresh runs, then replace with new results when refresh completes.
- Q: If refresh fails, how should error feedback appear? → A: Show an inline non-blocking error near refresh controls while keeping the current list visible.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View Only External Sources (Priority: P1)

As a user, I want the source list to exclude the current device so I do not accidentally select myself as a source.

**Why this priority**: Preventing self-selection removes confusion and avoids invalid or meaningless viewing attempts.

**Independent Test**: Can be tested by opening the view screen on a device that is visible on the network and confirming the device itself is not listed while other eligible sources are still listed.

**Acceptance Scenarios**:

1. **Given** the current device is discoverable as a source, **When** the user opens or refreshes the view screen, **Then** the current device is not shown in the available source list.
2. **Given** there are other available sources, **When** the source list is displayed, **Then** those other sources remain visible and selectable through their dedicated action buttons.

---

### User Story 2 - Start Viewing Through Explicit Action (Priority: P1)

As a user, I want a clear "view stream" button for each source so I can intentionally open a stream without accidental taps on list rows.

**Why this priority**: This is the primary interaction model for entering the viewer and directly impacts usability and error prevention.

**Independent Test**: Can be tested by tapping row areas and button areas separately to confirm only the "view stream" button starts viewing.

**Acceptance Scenarios**:

1. **Given** one or more sources are listed, **When** the user taps a non-button area of a source row, **Then** no navigation or stream open action occurs.
2. **Given** one or more sources are listed, **When** the user taps that source's "view stream" button, **Then** the app starts viewing that specific source.
3. **Given** the view screen is open, **When** the user checks available row actions, **Then** no standalone "start output" button is present on this screen.

---

### User Story 3 - Refresh Feedback and Placement (Priority: P2)

As a user, I want refresh controls to be easy to find and provide clear in-progress feedback so I understand when source discovery is running.

**Why this priority**: Clear refresh behavior improves confidence and prevents repeated or conflicting refresh actions.

**Independent Test**: Can be tested by triggering refresh and confirming button position, loading icon placement, and disabled state during refresh.

**Acceptance Scenarios**:

1. **Given** the view screen is shown, **When** the user looks at the control area, **Then** the refresh button is located at the bottom-left of the screen.
2. **Given** the user initiates refresh, **When** refresh is active, **Then** a loading icon appears next to the refresh button and the refresh button is disabled.
3. **Given** refresh has finished, **When** the view screen returns to idle state, **Then** the refresh button is enabled again and the loading icon no longer indicates active refresh.
4. **Given** a source list is already displayed, **When** refresh is active, **Then** the current list remains visible until refreshed results are available.
5. **Given** a source list is already displayed, **When** refresh fails, **Then** an inline non-blocking error appears near refresh controls and the current list remains visible.

### Visual Change Quality Gate *(mandatory when UI changes are present)*

- This feature changes visual behavior and MUST include Playwright end-to-end tests on emulator(s) covering:
  - filtering out the current device from source options,
  - button-only click behavior for entering stream view,
  - absence of output-start action on the view screen,
  - refresh button placement and refresh-state behavior.
- This feature MUST run the existing Playwright e2e suite and keep all existing tests passing.

### Edge Cases

- No sources available: the list remains empty and refresh behavior is still visible and usable.
- Only source present is the current device: no source options are shown to the user.
- User taps source row rapidly outside the button: no unintended view navigation occurs.
- User attempts repeated refresh taps while refresh is already active: additional refresh attempts are blocked until current refresh completes.
- Refresh takes longer than expected: loading indicator remains visible and button remains disabled until refresh completes or fails gracefully.
- Refresh fails due to transient network or discovery error: an inline non-blocking error is shown near refresh controls and the previously shown list remains visible.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST exclude the current device from the list of available sources shown on the view screen.
- **FR-002**: The system MUST provide a "view stream" button for every displayed source on the view screen.
- **FR-003**: The system MUST make only the "view stream" button clickable for starting stream viewing from a source row.
- **FR-004**: The system MUST prevent non-button row taps from initiating any navigation or stream action.
- **FR-005**: The system MUST NOT present a "start output" action button on the view screen.
- **FR-006**: The system MUST place the refresh button at the bottom-left area of the view screen.
- **FR-007**: The system MUST display a loading icon adjacent to the refresh button while refresh is in progress.
- **FR-008**: The system MUST disable the refresh button while refresh is in progress.
- **FR-009**: The system MUST re-enable the refresh button after refresh completes or fails.
- **FR-010**: While refresh is in progress, the system MUST keep the currently displayed source list visible.
- **FR-011**: For these visual behavior changes, the system MUST include emulator-run Playwright e2e coverage for all updated user-visible flows in this specification.
- **FR-012**: For these visual behavior changes, the system MUST execute and keep passing all existing Playwright e2e tests.
- **FR-013**: If refresh fails, the system MUST display an inline non-blocking error message near the refresh controls while keeping the currently displayed source list visible.

### Assumptions

- "Current device" means the local app instance running on the user's device and any source identity mapped to that instance.
- "Bottom-left" allows minor spacing adjustments required by safe areas and device form factors, while preserving clearly left-aligned placement near the bottom edge.
- "Loading icon next to refresh button" means visually adjacent in the same control area so users can immediately associate loading state with refresh.
- During refresh, source rows remain visible and are replaced only when refreshed results arrive.
- Output initiation remains available through the stream menu and is intentionally outside this screen's direct actions.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In acceptance testing, 100% of source lists on the view screen exclude the current device when that device is otherwise discoverable.
- **SC-002**: In usability testing, at least 95% of first-time users can start viewing a selected source in one intentional tap on "view stream" without accidental row-triggered actions.
- **SC-003**: In refresh interaction tests, 100% of active refresh states show a visible loading icon adjacent to the refresh button and keep the refresh button disabled until refresh ends.
- **SC-004**: In regression execution, all existing Playwright e2e tests remain passing and all newly added visual-flow tests for this feature pass.
- **SC-005**: In refresh failure tests, 100% of failures present an inline non-blocking error near refresh controls while preserving the currently visible source list.
