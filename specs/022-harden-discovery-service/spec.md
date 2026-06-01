# Feature Specification: Discovery Service Reliability

**Feature Branch**: `022-harden-discovery-service`  
**Created**: 2026-03-29  
**Status**: Draft  
**Input**: User description: "we need to evealuate the function of the NDI bridge and the Discovery Service implementation. The current implementation does not work properly because when I set one or more discovery severs now NDI streams are showing up. We need Additional loggin Additional information shown when the developer mode is turned on Add a connection check when an discovery server is added Add a recheck button for each registered discovery server"

## Clarifications

### Session 2026-03-29

- Q: What defines a successful discovery-server connection check? → A: NDI discovery-specific handshake/response succeeds for the server endpoint.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Validate Discovery Server Connectivity (Priority: P1)

As a developer configuring discovery servers, I can verify connectivity when adding a discovery server so I can detect configuration problems before relying on source discovery.

**Why this priority**: If server connectivity is invalid at registration time, discovery silently fails and blocks all downstream workflows.

**Independent Test**: Can be fully tested by adding a reachable server and an unreachable server, then confirming each add action returns a clear pass/fail connection result and diagnostic message.

**Acceptance Scenarios**:

1. **Given** developer mode is enabled and a user adds a valid discovery server endpoint, **When** the add action is submitted, **Then** the system performs a connection check and records a successful status with timestamp.
2. **Given** developer mode is enabled and a user adds an unreachable or invalid discovery server endpoint, **When** the add action is submitted, **Then** the system reports a failed connection check with a human-readable failure reason and guidance to retry.
3. **Given** multiple discovery servers are registered, **When** a new server is added, **Then** connection checking for the new server does not remove or overwrite existing server statuses.

---

### User Story 2 - Recheck Registered Servers (Priority: P2)

As a developer troubleshooting intermittent network issues, I can manually recheck each registered discovery server so I can confirm whether connectivity recovers without removing and re-adding servers.

**Why this priority**: Manual recheck shortens diagnosis time and avoids destructive reset workflows.

**Independent Test**: Can be tested by registering at least two servers and triggering recheck for one server; only the selected server should be revalidated and its status updated.

**Acceptance Scenarios**:

1. **Given** one or more servers are already registered, **When** the user selects recheck on a specific server, **Then** only that server is revalidated and its latest status and timestamp are updated.
2. **Given** a recheck is in progress for a server, **When** the check completes, **Then** the result is shown as success or failure with the latest diagnostic details.
3. **Given** recheck fails due to temporary network unavailability, **When** the network is restored and the user rechecks again, **Then** the server status updates to success without requiring server re-registration.

---

### User Story 3 - Developer Diagnostics for Discovery Flow (Priority: P3)

As a developer investigating missing NDI sources, I can view additional bridge/discovery diagnostics in developer mode so I can identify whether failures occur in registration, connectivity, or discovery refresh.

**Why this priority**: Enhanced diagnostics reduce time-to-resolution but are less critical than core connectivity validation.

**Independent Test**: Can be tested by toggling developer mode and confirming additional diagnostics become visible only when enabled, while baseline behavior remains unchanged when disabled.

**Acceptance Scenarios**:

1. **Given** developer mode is enabled, **When** discovery operations run, **Then** the UI shows expanded server diagnostics including last check result, last check time, and failure context.
2. **Given** developer mode is disabled, **When** discovery operations run, **Then** developer-only diagnostics are hidden and standard user-facing information remains.
3. **Given** discovery source lists remain empty after server registration, **When** the developer opens diagnostics, **Then** logs and status details clearly indicate whether the issue is connection failure, no sources available, or refresh timeout.

---

### Visual Change Quality Gate *(mandatory when UI changes are present)*

- This feature includes visual changes: per-server recheck controls and developer-mode diagnostic details in discovery server views.
- Emulator-run Playwright e2e tests MUST cover: add server with connection check, failed connection messaging, per-server recheck action, and developer-mode diagnostics visibility toggling.
- Full existing Playwright e2e regression suite MUST be executed and remain passing.

### Test Environment & Preconditions *(mandatory)*

- Required runtime dependencies:
- Android build/runtime prerequisites validated.
- NDI SDK installation available to the app runtime.
- At least one reachable discovery server endpoint and one unreachable/invalid endpoint for negative-path validation.
- Two Android emulators or equivalent device setup for end-to-end validation where required by existing regression harness.
- Preflight checks that MUST pass before e2e validation:
- `scripts/verify-android-prereqs.ps1`
- `scripts/verify-e2e-dual-emulator-prereqs.ps1`
- Blocked result handling:
- If external dependencies (NDI SDK missing, discovery server unreachable by test environment, emulator unavailable) prevent execution, mark gate as `BLOCKED: ENVIRONMENT`, include failed preflight/check output, and provide explicit unblock action (install SDK, restore network route, start required emulator instances).

### Edge Cases

- A discovery server endpoint is syntactically valid but reachable host does not run the expected discovery service.
- Multiple servers are registered and only a subset are reachable; unreachable statuses must not mask reachable server discovery.
- Recheck is triggered repeatedly in quick succession for the same server; system should avoid duplicate in-flight checks and show deterministic final status.
- Device network changes during add/recheck; result should reflect actual final connectivity state and include timestamp.
- Developer mode is toggled off after diagnostics were visible; developer-only details should be hidden immediately without removing stored status history.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST perform a discovery server connection check each time a new discovery server is added.
- **FR-001a**: A discovery server connection check MUST be considered successful only when an NDI discovery-specific handshake/response succeeds for the configured server endpoint.
- **FR-002**: System MUST store per-server connection status metadata including most recent result, most recent check timestamp, and most recent failure reason when applicable.
- **FR-003**: System MUST provide a per-server recheck action for every registered discovery server.
- **FR-004**: System MUST update only the targeted server record when a per-server recheck is executed.
- **FR-005**: System MUST expose additional diagnostic details when developer mode is enabled, including bridge/discovery operation state and server-specific check outcomes.
- **FR-006**: System MUST hide developer-only diagnostic details when developer mode is disabled.
- **FR-007**: System MUST emit additional logging for discovery server add, connection check start/end, recheck start/end, and discovery refresh outcomes.
- **FR-008**: Logging MUST include enough correlation context to trace a single server through add, check, and refresh outcomes.
- **FR-009**: If a connection check fails, system MUST provide actionable user feedback indicating that the server is registered but currently unreachable.
- **FR-010**: System MUST preserve registered discovery server entries even when individual checks fail, unless explicitly removed by user action.
- **FR-011**: For visual additions/changes, system MUST include emulator-run Playwright e2e coverage for all new/updated user-visible functionality.
- **FR-012**: For visual additions/changes, system MUST execute and keep passing all existing Playwright e2e tests.
- **FR-013**: Validation flows MUST run and record preflight checks before executing end-to-end or release gates.
- **FR-014**: Validation reporting MUST classify each failed/blocked gate as code failure or environment blocker with reproduction details.

### Key Entities *(include if feature involves data)*

- **Discovery Server Entry**: A user-registered discovery endpoint with attributes for address, registration timestamp, enabled state, and latest connectivity result.
- **DiscoveryServerCheckStatus** (Server Connectivity Check Result): A diagnostic record tied to one discovery server entry with attributes for check time, outcome (success/failure), failure classification, and diagnostic message.
- **DeveloperDiscoveryDiagnostics** (Developer Diagnostic Snapshot): A developer-visible summary of discovery workflow state, including last refresh attempt, result summary, and per-server connectivity rollup.

### Assumptions

- Registered discovery servers remain configurable in current settings surfaces; this feature extends diagnostics and controls without changing high-level navigation.
- Developer mode already exists and can gate additional information visibility.
- Connection check validates server reachability/response expectations at registration and recheck time, not guaranteed source presence.
- Connection-check success is based on discovery-service protocol response, not merely TCP reachability and not source presence.
- Existing persistence for discovery server configuration remains in place and is reused for connectivity metadata.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In validation runs, 95% of discovery server add operations return a visible connection check result (success/failure) within 5 seconds.
- **SC-002**: 100% of registered discovery servers display an available recheck action and update their individual status after recheck completion.
- **SC-003**: When developer mode is enabled, troubleshooting sessions can identify one of three root-cause categories (server unreachable, no sources discovered, refresh failure) within 2 minutes in at least 90% of tested incidents.
- **SC-004**: Across the first release cycle after rollout, discovery-related investigation time is reduced by at least 40% compared to the prior baseline measured from issue triage notes.
