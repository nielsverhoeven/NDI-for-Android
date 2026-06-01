# Feature Specification: NDI Discovery Routing Reliability

**Feature Branch**: `[031-fix-ndi-discovery-routing]`  
**Created**: 2026-04-26  
**Status**: Draft  
**Input**: User description: "we are still in trouble regarding the NDI discovery. We need to reiterate the implementation. When no discovery server is configured, then mulitcast traffic should be used to find NDI sources on the network. If one or more NDI discovery servers are configured, then the usage of mDNS should be disabled and the NDI Discovery Server should be used to fetch the stream urls and ports of the NDI sources that are available through the NDI discovery server. Thesesources should be reported back from the discovery service typically within 2 seonds, with a maximum of 5 seconds. Make sure to take the following into account:
- The NDI Discovery Server is only queried for the sources and retrieves the IP adresses and ports of the sources.
- It should take no longer thang 5 seconds to retrieve this information from the server.
- The retrieved sources should be stored in the database of the app for caching purposes."

## User Scenarios & Testing *(mandatory)*

> **IMPORTANT**: Each user story is implementable and testable independently **after Phase 2 shared infrastructure (models, contracts, persistence, wiring) is complete**. See Phase 2 tasks T007–T011 in tasks.md for prerequisites.

### User Story 1 - Multicast Fallback Discovery (Priority: P1)

As a user on a local network with no configured NDI discovery servers, I can still find available NDI sources through multicast/mDNS discovery so the app remains usable without extra setup.

**Why this priority**: This is the baseline discovery path; if this fails, users without a discovery server cannot use the app at all.

**Prerequisite**: Phase 2 tasks T007–T011 must be complete before US1 implementation begins.

**Independent Test**: Can be fully tested by clearing discovery-server configuration, launching source discovery on a network with multicast-capable NDI sources, and verifying sources appear and can be used.

**Acceptance Scenarios**:

1. **Given** no discovery servers are configured, **When** discovery is triggered, **Then** multicast/mDNS discovery is used and available NDI sources are returned.
2. **Given** no discovery servers are configured, **When** discovery completes, **Then** discovered sources are persisted in cache storage.

---

### User Story 2 - Discovery Server Directed Lookup (Priority: P2)

As a user in an environment with one or more configured NDI discovery servers, I receive source endpoint host/port data from discovery servers only, without mixed multicast results, so source routing is deterministic and enterprise-compatible.

**Why this priority**: Mixed discovery modes create inconsistent source lists and incorrect routing behavior in managed networks.

**Prerequisite**: Phase 2 tasks T007–T011 must be complete, and US1 implementation (Phase 3 T016–T022) must be validated before US2 implementation begins.

**Independent Test**: Can be fully tested by configuring one or more discovery servers, triggering discovery, and verifying that mDNS is disabled and returned source endpoint host/port data comes from discovery-server lookup.

**Acceptance Scenarios**:

1. **Given** one or more discovery servers are configured and enabled, **When** discovery is triggered, **Then** mDNS/multicast discovery is disabled for that run.
2. **Given** one or more discovery servers are configured and enabled, **When** discovery completes successfully, **Then** source endpoint host and port values are returned from discovery-server source records.
3. **Given** one or more discovery servers are configured and enabled, **When** a discovery run starts, **Then** the run returns within 5 seconds or fails with explicit timeout diagnostics.

---

### User Story 3 - Cached Source Reuse After Discovery (Priority: P3)

As a returning user, I see previously discovered sources from app cache quickly after relaunch/update, even before a new live discovery run completes.

**Why this priority**: Improves reliability and perceived performance after updates/restarts while reducing disruption from transient network delays.

**Prerequisite**: Phase 2 tasks T007–T011 must be complete, US1 and US2 implementations (Phases 3–4) must be validated before US3 implementation begins.

**Independent Test**: Can be fully tested by performing discovery, confirming cache persistence, relaunching/updating app with data preserved, and verifying cached sources are still present.

**Acceptance Scenarios**:

1. **Given** sources were discovered previously, **When** the app is reopened, **Then** cached sources are read from persistent storage and shown before live discovery finishes.
2. **Given** a new discovery run updates endpoint data, **When** persistence completes, **Then** cached source records are updated without losing previously stored source identity.

---

### Visual Change Quality Gate *(mandatory when UI changes are present)*

- Visual behavior changes are included (source list content/timing and cached-source visibility behavior).
- Playwright end-to-end tests MUST cover:
  - discovery with no configured discovery server (multicast path)
  - discovery with configured discovery server(s) and mDNS disabled
  - cache visibility after relaunch/update with preserved app data
- Existing Playwright e2e regression suites MUST be executed and remain passing.

### Test Environment & Preconditions *(mandatory)*

- Runtime dependencies:
  - Android emulator(s) or physical device(s) available through `adb`
  - reachable local-network NDI source(s) for multicast validation
  - reachable configured NDI discovery server(s) for server-driven validation
  - app data-preserving update path available for cache-retention checks
- Required preflight checks before e2e validation:
  - `pwsh ./scripts/verify-android-prereqs.ps1`
  - `adb devices`
- Blocked-result handling:
  - If discovery server is unreachable, record result as `BLOCKED (environment)` with server endpoint and failure timestamp.
  - If no NDI source is available on multicast network, record result as `BLOCKED (environment)` with network fixture details.
- Existing automated tests are regression protection and MUST remain unchanged unless this feature directly changes their expected behavior.
- Tests expected to need updates: discovery repository contract tests, source-list/home availability gating tests, and discovery timing/timeout acceptance coverage.

### Edge Cases

- Discovery servers are configured but all are disabled: system falls back to multicast/mDNS mode.
- Discovery servers are configured and enabled but request exceeds 5 seconds: run fails with timeout diagnostics and preserves prior cache rows.
- Discovery server returns duplicate source identities via different endpoints: system maintains one canonical source row and updates endpoint deterministically.
- Discovery server returns source list with missing host/port fields: invalid records are excluded and diagnostics include source identifiers.
- App relaunch occurs while network is unavailable: cached rows remain visible and marked with stale/last-seen metadata until next successful discovery.
- Source list update race between cache emission and live discovery completion: final list reflects latest successful discovery without dropping cached identities unexpectedly.

## Requirements *(mandatory)*

### Clarified Decision Rules

- Discovery mode precedence is deterministic per run:
  - If enabled discovery server count is `0`, use multicast/mDNS discovery.
  - If enabled discovery server count is `>= 1`, use discovery-server mode only and do not execute mDNS for that run.
- Typical 2-second discovery-server timing is a performance target, not a fallback trigger.
- The 5-second boundary is a hard timeout boundary for discovery-server mode.
- Endpoint authority in discovery-server mode is the source endpoint returned by discovery records, not the discovery server endpoint itself.

### Functional Requirements

- **FR-001**: System MUST use multicast/mDNS discovery when no discovery server is configured and enabled.
- **FR-002**: System MUST disable multicast/mDNS discovery for a discovery run when one or more discovery servers are configured and enabled.
- **FR-003**: System MUST query configured discovery server(s) only for source records and retrieve source endpoint host and port values.
- **FR-004**: System MUST NOT treat discovery server endpoint addresses as source streaming endpoints.
- **FR-005**: System MUST return discovery results from discovery-server mode within 5 seconds or emit a timeout failure with actionable diagnostics.
- **FR-006**: System MUST target typical discovery-server completion around 2 seconds under healthy network/server conditions.
- **FR-007**: System MUST persist discovered sources (including identity, display name, endpoint host/port, and last-seen metadata) to app database cache.
- **FR-008**: System MUST emit persisted cached-source rows on app relaunch/update before live validation/discovery completes.
- **FR-009**: System MUST update existing cached-source rows for rediscovered sources instead of creating duplicate canonical rows.
- **FR-010**: System MUST preserve cached rows across app restarts and app updates when app data is retained.
- **FR-011**: For visual behavior changes, system MUST include emulator-run Playwright e2e coverage for new and changed user-visible flows.
- **FR-012**: For visual behavior changes, system MUST execute and keep passing existing Playwright e2e regression coverage.
- **FR-013**: For environment-dependent validations, system MUST run and record preflight checks before e2e/release gates.
- **FR-014**: Validation reporting MUST classify failures as either code failure or environment blocker with reproduction details.
- **FR-015**: Existing automated tests MUST be preserved as regression protection and changed only when required by this feature's behavior updates.
- **FR-016**: Discovery mode selection MUST be evaluated at the start of each discovery run using current enabled-server configuration and applied for the entire run.
- **FR-017**: Discovery-server mode timeout failures MUST return an explicit timeout reason and must not silently fall back to multicast in the same run.
- **FR-018**: Canonical source identity conflicts (same identity, different endpoint) MUST update the existing canonical cached row using the newest discovery timestamp.
- **FR-019**: Discovery-server mode MUST record per-server response timing/timeout diagnostics sufficient to identify slow or unreachable servers.

### Key Entities *(include if feature involves data)*

- **Discovery Mode Configuration**: Represents whether discovery runs in multicast mode (no enabled discovery server) or discovery-server mode (one or more enabled servers).
- **Discovered Source Endpoint**: Represents a network-discoverable NDI source with canonical source identity, display name, endpoint host, endpoint port, and discovery timestamp.
- **Cached Source Record**: Represents persisted source data used for relaunch/update continuity, including identity, endpoint fields, and last-seen/validation metadata.
- **Discovery Run Result**: Represents output of each run, including mode used, completion duration, returned source count, and timeout/failure diagnostics.

### Non-Goals

- This feature does not change stream playback protocol behavior beyond endpoint selection.
- This feature does not add editable developer controls for cache mutation.
- This feature does not introduce mixed-mode discovery in a single run.

## Assumptions

- "Configured discovery server" means configured and enabled for use.
- Discovery server response timing targets apply to healthy network and server conditions.
- App updates used for validation preserve app data (non-uninstall update path).
- Source endpoint host/port returned by discovery server is authoritative for streaming target selection.
- If multiple discovery servers return the same canonical source identity, the newest source endpoint record is considered authoritative for cache update.

## Non-Functional Requirements *(mandatory)*

### Performance

- **NFR-Perf-001**: Discovery-server runs MUST complete ≥95% of time within 2 seconds under healthy network/server conditions (Measurable in SC-002).
- **NFR-Perf-002**: All discovery runs (multicast or discovery-server) MUST complete within 5 seconds or fail with explicit timeout diagnostics (Enforceable in FR-005, FR-017).
- **NFR-Perf-003**: Cached source rows MUST be emitted and visually rendered before live discovery completion on app relaunch/update (Validated in SC-004).

### Reliability

- **NFR-Rel-001**: Cached source rows MUST survive discovery timeouts and failures; prior cache remains visible with last-seen metadata (Verified in FR-010, SC-004).
- **NFR-Rel-002**: Canonical source identity conflicts (same source identity, different endpoint) MUST be resolved deterministically using newest discovery timestamp; existing cached row updated instead of duplicated (Verified in FR-018).
- **NFR-Rel-003**: Discovery mode selection MUST be deterministic: evaluated once per run and applied for entire run duration; no mid-run switching (Verified in FR-016).

### Diagnostics & Observability

- **NFR-Diag-001**: Per-server response timing and timeout diagnostics MUST be captured sufficient to identify slow or unreachable servers (Measured in FR-019).
- **NFR-Diag-002**: Discovery failures due to network/environment unavailability MUST be classified as environment blockers (not code failures) and documented with reproduction details (Classified in FR-014, SC-005).
- **NFR-Diag-003**: Discovery run results MUST record: mode used, completion duration, returned source count, and failure/timeout reason if applicable (Captured in FR-006, FR-017, FR-019).

### Testability & Compatibility

- **NFR-Test-001**: All new user-visible flows MUST be covered by emulator-run Playwright e2e tests; existing regression suites MUST remain passing (Required in FR-011, FR-012, FR-013).
- **NFR-Test-002**: Preflight checks (Android and Playwright environment validation) MUST run before e2e/release gates; environment blockers MUST be recorded separately from code failures (Required in FR-013, FR-014).
- **NFR-Compat-001**: Room database persistence for cached sources MUST be preserved across app restarts and updates when app data is retained (Verified in FR-010, SC-004).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In environments with no enabled discovery servers, users can discover at least one available network source through multicast/mDNS in manual validation sessions.
- **SC-002**: In environments with enabled discovery server(s), 95% of successful discovery runs complete within 2 seconds, and 100% of successful runs complete within 5 seconds. **Measurement Procedure**: Collect ≥50 discovery runs per test environment; record wall-clock completion time from run initiation to result emission (inclusive of all network I/O); calculate 95th and 100th percentiles; document sample environment conditions (server response time ≤500ms, client-to-server latency ≤100ms) in `test-results/031-us2-discovery-server-routing.md`.
- **SC-003**: In discovery-server mode, 100% of sampled stream/view starts use persisted source endpoint host/port data and 0% use discovery-server endpoint addresses.
- **SC-004**: After app relaunch/update with preserved app data, cached source rows appear before live discovery completion in 100% of validation runs where cache exists.
- **SC-005**: Discovery failures due to unavailable network fixtures or servers are clearly classified as environment blockers in 100% of validation reports.
