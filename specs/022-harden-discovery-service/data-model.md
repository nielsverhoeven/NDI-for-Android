# Data Model - Discovery Service Reliability

## Entity: DiscoveryServerEntry

Represents one user-configured discovery server endpoint.

Fields:
- id: string, primary key
- hostOrIp: string, required
- port: integer, required
- enabled: boolean, required
- orderIndex: integer, required
- createdAtEpochMillis: long, required
- updatedAtEpochMillis: long, required

Validation rules:
- hostOrIp must be non-blank and length-valid.
- port must be within 1..65535 (default 5959 when empty input).
- Combination of hostOrIp+port must be unique.

State transitions:
- Created -> Enabled/Disabled via toggle.
- Enabled/Disabled -> Reordered via drag/reorder action.
- Any -> Deleted via explicit user delete action.

## Entity: DiscoveryServerCheckStatus

Represents the latest connection-validation result for a specific server entry.

Fields:
- serverId: string, foreign-key-like reference to DiscoveryServerEntry.id
- checkType: enum { ADD_VALIDATION, MANUAL_RECHECK }
- outcome: enum { SUCCESS, FAILURE }
- checkedAtEpochMillis: long, required
- failureCategory: enum { NONE, ENDPOINT_UNREACHABLE, HANDSHAKE_FAILED, TIMEOUT, UNKNOWN }
- failureMessage: string, nullable
- correlationId: string, required for tracing logs across layers

Validation rules:
- SUCCESS requires failureCategory=NONE and empty failureMessage.
- FAILURE requires failureCategory != NONE and non-empty failureMessage.
- checkedAtEpochMillis must be monotonic per server (new result overwrites previous visible status).

State transitions:
- Unknown -> SUCCESS/FAILURE on add-time check.
- SUCCESS/FAILURE -> SUCCESS/FAILURE on manual recheck.
- Previous status is replaced by newest check result for the same server.

## Entity: DeveloperDiscoveryDiagnostics

Represents developer-visible diagnostics state for discovery and server checks.

Fields:
- developerModeEnabled: boolean
- latestDiscoveryRefreshStatus: enum { IN_PROGRESS, SUCCESS, EMPTY, FAILURE }
- latestDiscoveryRefreshAtEpochMillis: long, nullable
- serverStatusRollup: list of DiscoveryServerCheckStatus projections
- recentDiscoveryLogs: list of redacted diagnostic lines

Validation rules:
- When developerModeEnabled=false, developer-only diagnostics must be hidden from UI.
- Recent log entries must be redaction-safe before rendering.

State transitions:
- Hidden -> Visible when developer mode toggles on.
- Visible -> Hidden when developer mode toggles off.
- While visible, diagnostics update on add check, recheck, and discovery refresh events.

## Relationship Summary

- DiscoveryServerEntry has a 1:1 latest status projection in DiscoveryServerCheckStatus keyed by serverId.
- DeveloperDiscoveryDiagnostics aggregates all DiscoveryServerCheckStatus items and discovery refresh signals.
- UI list rows render entry fields from DiscoveryServerEntry and diagnostics fields from DiscoveryServerCheckStatus.

## Derived UI Fields

Computed per server row:
- canRecheck = true when entry exists
- checkBadgeText = "Connected" for SUCCESS, "Check failed" for FAILURE
- checkDetailText = timestamp + failure detail (if any)
- showDeveloperDetails = developerModeEnabled

Computed for overall diagnostics:
- categorizedFailureSummary by failureCategory
- latestOperationCorrelation for troubleshooting continuity
