# Feature Specification: Per-Source Last Frame Retention

**Feature Branch**: `023-per-source-frame-retention`  
**Created**: 2026-03-30  
**Status**: Draft  
**Input**: User description: "I want to update the functionality of the view screen. It now only retains the last frame from the last viewed source, but it should retain the last frame of all sources that are viewed at least once."

## Clarifications

### Session 2026-03-30

- Q: What uniquely identifies an NDI source for frame-keying purposes? → A: Opaque unique ID assigned by the NDI SDK (not display name or IP address).
- Q: At what point during a viewing session should the frame be captured? → A: Last frame displayed when the user exits the viewer.
- Q: How many sources' frames should the app retain before evicting the oldest? → A: Cap at 10 sources; when the cap is exceeded, the frame for the least-recently-viewed source is evicted.
- Q: Should retained frames survive a full app restart? → A: No — retain frames only within a single foreground session (in-memory); no disk persistence required.
- Q: What resolution should captured frames be stored at? → A: Thumbnail resolution (~320×180) sufficient for source list row display.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See Last Frame Per Source in Source List (Priority: P1)

As a user browsing the source list, I want to see the last captured frame for each source I have previously viewed so I can quickly identify and re-open a familiar source without starting a new stream first.

**Why this priority**: This is the core value of the feature — surfacing per-source frame previews in the source list is the change users will immediately notice and benefit from. It directly replaces the broken single-frame behavior.

**Independent Test**: Can be fully tested by viewing two distinct sources in sequence, returning to the source list, and confirming that both sources display their own individual last-captured frame as a thumbnail or placeholder.

**Acceptance Scenarios**:

1. **Given** source A has been viewed at least once, **When** the user returns to the source list, **Then** source A's row displays the last captured frame from that viewing session.
2. **Given** source B has also been viewed at least once, **When** the user returns to the source list, **Then** source B's row displays source B's own last captured frame, independent of source A.
3. **Given** source C has never been viewed, **When** the user looks at source C's row in the source list, **Then** no frame is shown and a neutral placeholder is displayed instead.
4. **Given** the user viewed source A first and then source B, **When** checking the source list, **Then** source A still retains its own last frame (not overwritten by source B's frame).

---

### User Story 2 - Frame Retention Persists Across Navigation (Priority: P1)

As a user, I want the per-source last frame to still be visible when I navigate away from the viewer and come back to the source list, so previously viewed sources always show their frame regardless of what I did since then.

**Why this priority**: Without persistence across navigation, the feature provides no advantage over the current behavior. Frame retention must survive navigation events to be useful.

**Independent Test**: Can be tested by viewing a source, navigating to another screen (e.g., settings or output), returning to the source list, and confirming the previously viewed source still shows its last frame.

**Acceptance Scenarios**:

1. **Given** source A has been viewed and a frame was captured, **When** the user navigates away and returns to the source list, **Then** source A's last frame is still displayed in its row.
2. **Given** multiple sources have been viewed, **When** the user navigates between screens and returns to the source list, **Then** all previously viewed sources still show their individual last frames.
3. **Given** the user opens the viewer for source A a second time, **When** a new frame is available during this second session, **Then** the source list updates source A's thumbnail to the newly captured frame after the user exits the viewer.

---

### User Story 3 - Frame Cleared When Source Is No Longer Available (Priority: P2)

As a user, I want the view screen to gracefully handle sources that disappear from the network so that stale frame data does not cause confusion.

**Why this priority**: Secondary UX concern — stale frame data for disconnected sources may mislead the user into believing the source is still available.

**Independent Test**: Can be tested by viewing a source, disconnecting it from the network, refreshing the source list, and confirming the stale frame is no longer shown.

**Acceptance Scenarios**:

1. **Given** source A has a retained last frame and then becomes unavailable, **When** the source list is refreshed and source A no longer appears, **Then** source A's row and its retained frame are removed from the list.
2. **Given** source A re-appears after being offline, **When** the source list refreshes and shows source A again, **Then** source A's previously retained frame is shown until a new viewing session replaces it.

---

### Visual Change Quality Gate *(mandatory when UI changes are present)*

- This feature changes visual behavior and MUST include Playwright end-to-end tests on emulator(s) covering:
  - per-source last frame display in the source list after viewing multiple sources,
  - neutral placeholder shown for sources never viewed,
  - frame retention surviving navigation between screens.
- This feature MUST run the existing Playwright e2e suite and keep all existing tests passing.

### Test Environment & Preconditions *(mandatory)*

- Two emulators (or one emulator plus one physical device) both reachable on the same network to simulate at least two distinct NDI sources.
- NDI SDK prerequisites confirmed present (`scripts/verify-android-prereqs.ps1`).
- Preflight command: `scripts/verify-e2e-dual-emulator-prereqs.ps1` — confirms both endpoints are live and NDI discovery is reachable before tests run.
- If only one source is available at test time, tests covering multi-source retention MUST be recorded as **blocked (environment)** with the missing source listed as the unblocking step.

### Edge Cases

- Source list is empty and no sources have ever been viewed: no frames are shown and the list renders normally with placeholders.
- User views the same source many times in succession: only the latest captured frame is retained for that source (one frame per source, not a history).
- A source's NDI SDK-assigned opaque ID changes across network appearances (e.g., source fully reinstalled): the system treats it as a new source with no prior frame. Display name or IP changes alone do not invalidate a retained frame as long as the opaque ID is unchanged.
- The 10-source cap is reached: the frame belonging to the least-recently-viewed source is silently evicted to make room; no user-visible error is raised.
- Device storage is critically low: frame data capture degrades gracefully without crashing; the feature operates on a best-effort basis without blocking stream viewing.
- User rapidly switches between multiple sources: each source independently captures and retains its own frame without race conditions discarding frames from other sources.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST retain the last frame displayed at viewer-exit time separately for each NDI source, keyed by the NDI SDK-assigned opaque source ID.
- **FR-002**: The retained frame for a source MUST be displayed in that source's row in the source list at thumbnail resolution with max width 320 pixels; height MUST be computed to preserve the source's original aspect ratio.
- **FR-003**: Sources that have never been viewed MUST display a neutral placeholder instead of a frame.
- **FR-004**: The retained frame for a given source MUST NOT be overwritten by viewing a different source.
- **FR-005**: Retained frames MUST persist across navigation events within the session (navigating away and back to the source list does not clear them).
- **FR-006**: When a source is no longer visible in the source list after a refresh, its retained frame MUST also be removed from view.
- **FR-007**: When the user views a source for a second or subsequent time, the retained frame for that source MUST be updated to the last frame displayed when the user exits that viewing session.
- **FR-012**: The system MUST retain frames for at most 10 sources concurrently; when a new source is viewed and the cap is reached, the frame for the least-recently-viewed source MUST be evicted.
- **FR-013**: Retained frames are in-memory only and MUST NOT be written to disk; they are discarded when the app process ends.
- **FR-008**: The system MUST include emulator-run Playwright e2e coverage for the multi-source frame retention flow.
- **FR-009**: All existing Playwright e2e tests MUST continue to pass after this change.
- **FR-010**: Preflight checks MUST be run and recorded before end-to-end or release gate validation.
- **FR-011**: Validation reports MUST classify each failed or blocked gate as either a code failure or an environment blocker with reproduction details.

### Key Entities

- **Source Last Frame**: The last frame displayed at viewer-exit time, stored at thumbnail resolution (~320×180), associated with a single NDI source keyed by its SDK-assigned opaque ID; replaces the previous single global last-frame store.
- **Source Identifier**: The opaque unique ID assigned by the NDI SDK — the only stable key used to associate a retained frame with a specific source. Display name and IP address are not part of the key.
- **Frame Placeholder**: The neutral visual element shown in a source row when no frame has been retained for that source.
- **Retention Cap**: The maximum number of per-source frames held in memory at once (10). When exceeded, the frame for the least-recently-viewed source is evicted.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After viewing two or more sources, all previously viewed sources display their own individual last frame in the source list without any frame being overwritten by another source.
- **SC-002**: A retained frame for a given source survives at least one full navigation round-trip (leave source list, return) without being lost.
- **SC-003**: Sources that have never been viewed show a placeholder, and no frame from another source appears in their row.
- **SC-004**: Viewing a source a second time replaces the retained frame for that source with the last frame displayed at exit of that second viewing session.
- **SC-005**: Frame retention does not increase the time for the source list to become interactive by more than two seconds on a supported device. Measured by recording source-list-visible timestamp with no frames retained vs. with all 10-source cap frames retained and comparing application startup times.

## Assumptions

- The NDI SDK assigns an opaque unique ID to each source that is stable for the lifetime of that source's network presence; this ID is used as the frame-map key.
- The current viewer already captures frames during playback; this feature changes how captured frames are stored (per-source map keyed by SDK ID, thumbnail resolution, in-memory only).
- Frame retention is scoped to the current foreground app session and discarded on process end; disk persistence is out of scope for this feature.
- One frame per source is sufficient; no frame history or timeline is required.
- A cap of 10 concurrently retained source frames is a reasonable upper bound given typical NDI studio environments; this can be revisited if larger deployments require it.
