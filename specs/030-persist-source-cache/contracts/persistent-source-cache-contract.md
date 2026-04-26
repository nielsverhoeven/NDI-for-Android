# Contract: Persistent Source Cache and Validation UX

## 1. Repository Contracts

### 1.1 Cached source persistence

- Discovery-related repository logic MUST upsert one canonical cached-source record per source identity.
- Canonical identity MUST be `stableSourceId ?: normalizedEndpoint`.
- Display name changes MUST update the existing row and MUST NOT create a new row.
- Cached-source persistence MUST retain endpoint host/port, preview-image path reference, validation state, and associated discovery-server IDs.
- Missing preview files MUST degrade to a placeholder state without deleting the cached-source row.

### 1.2 Discovery merge behavior

- On app launch or screen entry, repository flows consumed by Home/View MUST emit persisted cached-source rows before live validation completes.
- When validation begins for a cached row, its exposed state MUST become `VALIDATING` until a success or failure result is known.
- When live discovery returns the same source through multiple discovery servers, repository logic MUST retain one canonical cached-source row plus multiple server associations.
- When one or more discovery servers are enabled, discovery metadata MUST come only from enabled discovery servers.
- Discovery-server endpoints MUST NOT be used as viewer/output stream targets.

### 1.3 Stream-start endpoint resolution

- Viewer/output startup for discovery-populated sources MUST resolve the stored source endpoint from the cached-source record.
- If the persisted endpoint has changed due to rediscovery, the newest persisted source endpoint MUST be used.
- If no valid source endpoint is available, the repository MUST fail safely with actionable diagnostics rather than falling back to the discovery-server endpoint.

## 2. Home and View UI Contracts

### 2.1 Cached source presentation

Input signals per cached source:

- canonical identity
- display name
- retained preview reference
- validation state
- discovery provenance (diagnostics only)

Required outputs:

- Cached rows are visible immediately when persisted data exists, before live validation completes.
- A retained preview image is shown when the persisted preview path resolves successfully.
- If the preview file is missing, the UI shows the normal placeholder state while preserving the cached row.

### 2.2 View action enablement

- View/open controls for a cached source MUST be disabled when `validationState == VALIDATING`.
- View/open controls for a cached source MUST remain disabled when `validationState == UNAVAILABLE`.
- View/open controls for a cached source MUST be enabled only when `validationState == AVAILABLE`.
- Tapping a disabled View/open control MUST NOT trigger navigation or stream startup.

### 2.3 Home dashboard behavior

- Home dashboard snapshot data MUST reuse persisted cached-source information for the selected source when available.
- `canNavigateToView` MUST be false while the selected cached source is validating or currently unavailable.
- Home dashboard text for the selected source MUST remain populated from cached data even when the source is not yet validated in the current session.

## 3. Developer Inspection Contracts

- The database inspection surface MUST be visible only when developer mode is enabled.
- The inspection surface MUST be read-only.
- The inspection surface MUST show, at minimum:
  - canonical source identity
  - display name
  - stored source endpoint
  - validation state
  - preview-image path reference
  - associated discovery-server IDs or endpoints
  - key timestamps for discovery/validation recency
- Turning developer mode off MUST hide the inspection surface again without deleting stored data.

## 4. Test and Validation Contracts

- JUnit coverage MUST be added first for canonical-key upsert behavior, validation-state transitions, and Home/View enablement decisions.
- Persistence regression tests MUST prove existing settings/discovery tables remain intact after the Room migration.
- Playwright emulator e2e MUST cover:
  - cached sources shown before validation completes
  - disabled View action during validation
  - unavailable cached source remaining visible but non-actionable
  - developer inspection visibility gated by developer mode
- Existing Playwright regression flows MUST be executed and remain passing.
- Preflight commands MUST run before emulator/device gates:
  - `pwsh ./scripts/verify-android-prereqs.ps1`
  - `pwsh ./scripts/verify-e2e-dual-emulator-prereqs.ps1` when the dual-emulator harness is used
  - `adb devices`

## 5. Failure and Fallback Contracts

- Environment blockers (missing emulator, unreachable discovery server, no NDI source) MUST be reported separately from code failures.
- Cache rows MUST survive app restarts and updates even if current validation fails.
- A failed validation result MUST update UI state to unavailable, not remove the cached source silently.
- Developer inspection failures MUST fail closed and visible to diagnostics, without exposing an editing path or crashing Settings.