# Feature Specification: Persistent Source Cache

**Feature Branch**: `[030-persist-source-cache]`  
**Created**: 2026-04-19  
**Status**: Draft  
**Input**: User description: now I start to see what is going wrong. I want you to draft a new specification with the following information:

- A proper database with persistant data across app closures and updates should be created. This database should retain the sources that were once discovered including the last captured image of the NDI stream and the Discvery Server that was used to discover the device (if applicable). This data is to be used in the view menu to serve a caching info while the app is validating the availability of the sources.
- While the app is validating if the sources are availble, the button to start viewing the stream should be greyed out.
- When the developer mode is enabled, I should get an additional option in the developer part of the setting menu to view the data that is currently in the application database for me to validate.
- When at least one discovery service is enabled, only the set discovery servers should be used to retrieve NDI streams. Up untill now you have been trying to start streams throug the discovery service but that is not how it works. You should only retrieve the ip's and ports of the sources that were populated on the discovery service and then store them in the database for you. Those IP's are needed to set-up the streams once a user select so stream one.

## Clarifications

### Session 2026-04-19

- Q: Which identity rule should the spec use for deduplicating and persisting discovered sources? → A: Use the source endpoint as canonical, with stable SDK/source ID when available.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Cached Source Availability In View Menu (Priority: P1)

As an operator opening the View menu, I want previously discovered sources to remain visible with cached identity and preview information while the app revalidates them so I can understand what is expected to be available before live validation completes.

**Why this priority**: Operators need continuity across app restarts and updates. Without retained source data, the app appears empty during validation and users lose context about known sources.

**Independent Test**: Can be fully tested by discovering at least one source, closing and relaunching the app, opening the View menu, and confirming cached source information is shown immediately while live availability validation runs in the background.

**Acceptance Scenarios**:

1. **Given** the app has previously discovered one or more sources, **When** the app is relaunched or reopened after an update, **Then** the View menu immediately shows the cached sources with their retained preview image and last known source details before live validation finishes.
2. **Given** a cached source is being revalidated, **When** the user sees that source in the View menu, **Then** the source clearly indicates that availability is still being checked and the start-viewing action remains disabled until validation completes.
3. **Given** a cached source is no longer available, **When** validation completes, **Then** the cached entry remains identifiable as a previously known source but is updated to show that it is currently unavailable.

---

### User Story 2 - Discovery-Server-Sourced Stream Endpoints (Priority: P2)

As an operator using configured discovery servers, I want the app to treat discovery servers as source registries only and store the announced source endpoints so that stream launch always uses the source device IP and port rather than the discovery server address.

**Why this priority**: The current confusion between discovery servers and source endpoints leads to incorrect connection behavior. The app must preserve the source endpoint that was discovered and use that endpoint for stream start.

**Independent Test**: Can be fully tested by configuring one or more discovery servers, running discovery, and verifying that the stored source records contain the source device endpoint and that stream start uses the stored source endpoint instead of the discovery server address.

**Acceptance Scenarios**:

1. **Given** at least one discovery server is enabled, **When** the app retrieves available sources, **Then** it uses only the enabled discovery servers to obtain source metadata and stores each discovered source endpoint for later stream launch.
2. **Given** a user selects a source that was populated by a discovery server, **When** the user starts viewing the stream, **Then** the app connects to the stored source IP and port for that source rather than the discovery server endpoint.
3. **Given** multiple discovery servers are enabled, **When** the same source appears through more than one server, **Then** the stored source record preserves a single usable source entry keyed by source endpoint, or by a stable SDK/source ID when one is available, while retaining the discovery-server context needed for diagnostics.

---

### User Story 3 - Developer Database Inspection (Priority: P3)

As a developer with developer mode enabled, I want a settings option that displays the current application database contents relevant to discovery and cached sources so I can validate what the app has stored without external tools.

**Why this priority**: The feature depends on retained source and discovery metadata. Developers need a direct in-app validation surface to verify that stored data matches observed app behavior.

**Independent Test**: Can be fully tested by enabling developer mode, opening the developer section in settings, and confirming that the database inspection option shows the current stored source, preview, and discovery-server data.

**Acceptance Scenarios**:

1. **Given** developer mode is disabled, **When** a user opens settings, **Then** no database inspection option is shown.
2. **Given** developer mode is enabled, **When** a developer opens the developer section in settings, **Then** an option is available to inspect the current application database contents related to cached sources and discovery metadata.
3. **Given** stored source data changes after discovery or validation, **When** the developer reopens the database inspection view, **Then** the displayed data reflects the current stored state.

---

### Visual Change Quality Gate *(mandatory when UI changes are present)*

- This feature changes visible behavior in the View menu and Settings screens, so emulator-run Playwright end-to-end coverage is required for cached-source presentation, disabled stream actions during validation, and developer database inspection.
- Existing Playwright end-to-end suites must be executed after implementation and remain passing.
- Validation must cover both a cold-start flow with cached data already present and a first-run flow where no cached data exists yet.

### Test Environment & Preconditions *(mandatory)*

- **Required runtime dependencies**:
  - Android SDK prerequisites installed and passing repository preflight checks.
  - At least one Android emulator or device capable of running the debug build.
  - A reachable NDI discovery-server environment with at least one discoverable source.
  - A test setup that allows app restart or update simulation while preserving application data.
- **Preflight check**: Run `pwsh ./scripts/verify-android-prereqs.ps1` before emulator or device validation and record the result.
- **Preflight check**: Run `pwsh ./scripts/verify-e2e-dual-emulator-prereqs.ps1` before emulator-driven end-to-end validation when the selected test flow requires the existing e2e harness.
- **Preflight check**: Confirm the target runtime is visible in `adb devices` and that at least one configured discovery server can be reached before feature validation begins.
- **Blocked result handling**: If discovery servers, NDI sources, or emulator prerequisites are unavailable, the run must be marked blocked with the exact failed precondition and the concrete unblocking action.
- Existing automated tests covering View, discovery, and Settings behavior are expected regression protection; they should remain unchanged unless the requested behavior explicitly changes their contract, in which case the spec-to-test traceability must be recorded.

### Edge Cases

- A cached source remains stored after an app update, but the source endpoint is no longer reachable when validation runs.
- A source is rediscovered through a different discovery server than the one that originally populated it.
- A source is rediscovered with changed endpoint details, requiring the stored endpoint and availability state to be updated without creating a duplicate entry.
- No discovery servers are enabled, so the app must avoid implying that discovery-server-backed source validation is active.
- Developer mode is enabled on a device with many cached sources, and the inspection view must remain readable enough to validate stored records.
- The app has never discovered any sources before, so the View menu must show an empty-state experience rather than stale placeholders.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST maintain application data for discovered sources across app closures and app updates.
- **FR-002**: The stored source data MUST retain, for each known source, its source identity, last known source endpoint, latest retained preview image, most recent availability state, and the discovery server that supplied the source when applicable.
- **FR-003**: The View menu MUST present retained source data immediately on entry so users can see cached source information while live validation is still in progress.
- **FR-004**: While a cached source is being validated for current availability, the control that starts viewing that source MUST be visibly disabled.
- **FR-005**: Once a validation pass completes for a cached source entry, the system MUST update that entry to reflect either currently available or currently unavailable.
- **FR-005a**: Before validation completes in the current session, the system MUST represent cached source state as not yet validated or validating.
- **FR-006**: When at least one discovery server is enabled, the system MUST use only the enabled discovery servers to retrieve source metadata.
- **FR-007**: Discovery servers MUST be treated as registries for source metadata and MUST NOT be treated as stream endpoints.
- **FR-008**: For each source returned through discovery-server retrieval, the system MUST store the announced source IP and port needed to start the stream.
- **FR-009**: When a user starts viewing a source discovered through a discovery server, the system MUST use the stored source IP and port for stream startup.
- **FR-010**: When the same source is discovered again, the system MUST update the existing stored source record rather than creating duplicate active records for the same source identity.
- **FR-010a**: The canonical identity for a stored source MUST be the source endpoint, except that a stable SDK/source ID MUST take precedence when it is available.
- **FR-010b**: Display name alone MUST NOT be used as the canonical identity for deduplicating or persisting discovered sources.
- **FR-011**: The system MUST preserve which discovery server or servers were associated with the stored source so that operators and developers can validate how the source was obtained.
- **FR-012**: When developer mode is enabled, the developer section of Settings MUST expose an option to inspect the current application database contents relevant to retained sources and discovery metadata.
- **FR-013**: When developer mode is disabled, the database inspection option MUST NOT be shown.
- **FR-014**: The database inspection view MUST show the currently stored source records, retained preview references, and discovery-server associations in a form that developers can use to validate the application state.
- **FR-015**: For visual additions or changes in this feature, the system MUST include emulator-run Playwright end-to-end coverage for the updated View and Settings flows.
- **FR-016**: For visual additions or changes in this feature, the system MUST execute the existing Playwright end-to-end regression suite and keep it passing.
- **FR-017**: For environment-dependent validations, the system MUST run and record preflight checks before end-to-end validation begins.
- **FR-018**: Validation reporting MUST classify each failed or blocked gate as either a feature failure or an environment blocker with reproduction details.
- **FR-019**: Existing automated tests MUST be preserved as regression protection; they MAY be changed only when this feature directly changes the covered behavior or the test is separately proven invalid.
- **FR-020**: Any change to a pre-existing automated test MUST document the specific feature requirement or behavior change that made the update necessary.

### Key Entities *(include if feature involves data)*

- **Cached Source Record**: A retained representation of a previously discovered NDI source, including canonical source identity, last known stream endpoint, validation status, time-based recency information, and any stable SDK/source ID when available.
- **Cached Preview Snapshot**: The retained last captured image associated with a cached source and used to provide visual context before current availability is confirmed.
- **Discovery Server Association**: The recorded relationship between a cached source and the discovery server or servers that supplied that source's metadata.
- **Database Inspection Dataset**: The developer-visible collection of stored discovery and cached-source records used to validate application state.

## Assumptions

- Cached source records are intended to help users understand expected availability and are not a guarantee that a source can be viewed without current validation.
- A source endpoint is the default canonical identity for persistence, and a stable SDK/source ID overrides endpoint-only matching when it is available from discovery results.
- The retained preview image is expected to represent the latest successful capture known to the app, even if the source is currently unavailable.
- Developer database inspection is intended for validation and troubleshooting, not for direct editing of stored records.
- When no discovery servers are enabled, this feature does not require the app to infer source endpoints from unsupported discovery paths.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In validation runs with previously discovered sources, 100% of cached sources appear in the View menu before live availability validation completes after app relaunch.
- **SC-002**: In validation runs where a source is still being checked, 100% of start-viewing controls for that source remain disabled until validation finishes.
- **SC-003**: In validation runs using enabled discovery servers, 100% of successful stream starts for discovery-server-populated sources use the stored source endpoint rather than a discovery-server endpoint.
- **SC-004**: In developer-mode validation runs, the database inspection option is visible in 100% of enabled cases and hidden in 100% of disabled cases.
- **SC-005**: In a release validation cycle, all newly required Playwright coverage for cached View behavior and developer inspection passes, and existing Playwright regression suites remain green.
