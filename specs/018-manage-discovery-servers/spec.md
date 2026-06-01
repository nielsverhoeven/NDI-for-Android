# Feature Specification: Discovery Server Settings Management

**Feature Branch**: 018-manage-discovery-servers  
**Created**: 2026-03-28  
**Status**: Draft  
**Input**: User description: "I want to update the way I can add and manage discovery servers with a dedicated settings submenu, separate hostname and port input, default port behavior, multiple servers, and per-server enable or disable control."

## Clarifications

### Session 2026-03-28

- Q: When multiple discovery servers are enabled, what runtime selection behavior should be used? -> A: Use enabled servers in list order, trying each in sequence and failing over to the next enabled server if unreachable.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Add Discovery Servers from Settings (Priority: P1)

A user opens Settings and enters a dedicated discovery server management submenu where they can add a server by providing hostname or IP address and, optionally, a port.

**Why this priority**: Adding servers is the core capability required to configure discovery behavior.

**Independent Test**: Open the discovery server submenu, add a server with hostname only, and verify it appears in the managed server list with default port behavior.

**Acceptance Scenarios**:

1. **Given** the user is on Settings, **When** they open the discovery server submenu, **Then** they can see inputs for hostname or IP and a separate port field.
2. **Given** the user enters hostname or IP and leaves the port empty, **When** they save the server, **Then** the server is saved with port 5959.
3. **Given** the user enters hostname or IP and a valid port, **When** they save the server, **Then** the server is saved with the provided port.
4. **Given** the user attempts to save without a hostname or IP, **When** they submit, **Then** the system blocks the save and shows a clear validation message.

---

### User Story 2 - Manage Multiple Discovery Servers (Priority: P2)

A user can maintain multiple discovery servers in one place, allowing them to keep different endpoints ready without re-entering details each time.

**Why this priority**: Multi-server management is essential for users who switch between networks or environments.

**Independent Test**: Add at least three unique servers and verify all are persisted and displayed in the list.

**Acceptance Scenarios**:

1. **Given** one server already exists, **When** the user adds another unique server, **Then** both servers are visible in the list.
2. **Given** multiple servers exist, **When** the user leaves and reopens the app, **Then** the same servers remain listed.
3. **Given** a server with the same hostname and port already exists, **When** the user attempts to add it again, **Then** the system prevents duplicate creation and explains why.

---

### User Story 3 - Enable and Disable Individual Servers (Priority: P3)

A user can turn each discovery server on or off independently without removing it, so they can quickly choose which servers should participate.

**Why this priority**: Per-server toggles provide operational control while preserving saved configurations.

**Independent Test**: With multiple servers in the list, toggle one off and another on, then verify toggle states are preserved and reflected in behavior.

**Acceptance Scenarios**:

1. **Given** multiple servers are listed, **When** the user disables one server, **Then** that server is marked disabled while others keep their current states.
2. **Given** a server is disabled, **When** the user re-enables it, **Then** it returns to enabled state without requiring re-entry.
3. **Given** enabled or disabled states are configured, **When** the app restarts, **Then** each server retains its previous on or off state.
4. **Given** multiple servers are enabled in a defined order, **When** the first enabled server is unreachable during discovery usage, **Then** the system attempts the next enabled server in order.

---

### User Story 4 - Edit and Remove Discovery Servers (Priority: P2)

A user can edit the details of an existing discovery server or remove it entirely, and can reorder the list to control failover priority without re-adding entries.

**Why this priority**: Edit, delete, and reorder complete the management lifecycle. Without them users must remove and re-add entries to correct mistakes, making the feature frustrating to use.

**Independent Test**: Edit an existing server's port, remove a different server, drag a third server to a new position, relaunch the app, and verify the modifications persist correctly.

**Acceptance Scenarios**:

1. **Given** a server exists in the list, **When** the user edits its hostname or port and saves, **Then** the updated values replace the previous values and the server remains in its original list position.
2. **Given** a server exists, **When** the user removes it, **Then** it is immediately removed from the list and does not reappear after app restart.
3. **Given** multiple servers exist, **When** the user drags a server row to a new position, **Then** the list reflects the new order and that order persists after app restart.
4. **Given** the user edits a server to match an existing server's hostname and port, **When** they attempt to save, **Then** the system blocks the duplicate update and shows a clear validation message.

---

### Visual Change Quality Gate *(mandatory when UI changes are present)*

- This feature includes visual behavior changes and MUST add emulator-run Playwright tests for: opening the submenu, adding servers with and without explicit port, and per-server toggle updates.
- This feature MUST run all existing Playwright end-to-end tests and keep them passing.

### Test Environment & Preconditions *(mandatory)*

- Runtime dependencies: Android emulator or device with app installed, network path that supports discovery server validation, and NDI SDK prerequisites satisfied.
- Required preflight check: scripts/verify-android-prereqs.ps1 must pass before running end-to-end validation.
- If network dependencies prevent discovery validation, test reporting MUST mark the run as blocked by environment and include concrete unblocking steps (for example, provide reachable server endpoint and retry).

### Edge Cases

- Port input left empty: system applies default port 5959 automatically.
- Port input contains invalid values (non-numeric, negative, or outside valid port range): save is blocked with a clear correction message.
- Hostname or IP includes leading or trailing spaces: value is normalized before validation.
- User disables all servers: system remains functional and uses fallback discovery behavior already defined by existing product behavior.
- Saved server becomes unreachable: server remains saved and toggleable, and failures are surfaced during discovery usage rather than during simple list display.
- User edits a server to match an existing entry: duplicate validation prevents the update with clear feedback.
- User attempts to remove the last server: removal is allowed; the list becomes empty and existing fallback discovery behavior applies.
- Multiple enabled servers with partial outage: system tries enabled servers in persisted order and continues until one succeeds or all fail.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Settings MUST include a dedicated discovery server management submenu.
- **FR-002**: The discovery server add flow MUST provide separate input fields for hostname or IP and port.
- **FR-003**: The hostname or IP field MUST be required to save a discovery server.
- **FR-004**: If the port field is omitted, the system MUST save and use port 5959.
- **FR-005**: If a valid port is provided, the system MUST save and use the provided port value.
- **FR-006**: Users MUST be able to add multiple discovery servers.
- **FR-007**: The system MUST prevent exact duplicate server entries and provide clear feedback when a duplicate is attempted.
- **FR-008**: Users MUST be able to enable or disable each discovery server independently.
- **FR-009**: Enabled or disabled state for each server MUST persist across app restarts.
- **FR-010**: Saved discovery servers (hostname or IP, port, and toggle state) MUST persist across app restarts.
- **FR-011**: When multiple servers are enabled, discovery usage MUST attempt enabled servers in user-visible list order.
- **FR-012**: If an enabled server is unreachable, discovery usage MUST fail over to the next enabled server in order.
- **FR-013**: If all enabled servers are unreachable, the system MUST surface a clear discovery failure result.
- **FR-014**: For this visual change, emulator-run Playwright end-to-end coverage MUST be added for new discovery server management flows.
- **FR-015**: For this visual change, all existing Playwright end-to-end tests MUST continue to pass.
- **FR-016**: Preflight checks MUST be executed and recorded before environment-dependent validation gates.
- **FR-017**: Validation reports MUST classify failures as product defects or environment blockers with reproduction details.
- **FR-018**: Users MUST be able to edit the hostname or IP and port of an existing discovery server and save an updated entry.
- **FR-019**: Users MUST be able to remove a discovery server from the managed list permanently.
- **FR-020**: Users MUST be able to reorder discovery servers in the list, and the new order MUST persist across app restarts.

### Key Entities *(include if feature involves data)*

- **Discovery Server Entry**: Represents one configured discovery endpoint with hostname or IP, port, and enabled or disabled state.
- **Discovery Server Collection**: Represents the user-managed ordered list of discovery server entries and uniqueness constraints.
- **Discovery Server Settings View**: Represents the settings submenu that displays, validates, adds, edits, removes, and reorders discovery server entries.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: At least 95% of users can add a new discovery server from Settings without assistance on first attempt.
- **SC-002**: 100% of saved servers without an explicit port are persisted with port 5959.
- **SC-003**: Users can add at least five distinct discovery servers in a single session with no data loss after app restart.
- **SC-004**: 100% of per-server enable or disable changes remain consistent after app restart.
- **SC-005**: End-to-end validation confirms all new discovery server management flows pass while existing Playwright suites remain passing.

## Assumptions

- Discovery server management is available to all users who can access Settings.
- Network reachability checks occur during discovery usage flow and are outside the scope of basic add, edit, or delete settings interactions.
- Ordered failover is applied only across enabled servers in the persisted list order.
- Edit, delete, and reorder are considered core management operations for a multi-server settings screen: the user's original request for a management submenu implies these capabilities.
