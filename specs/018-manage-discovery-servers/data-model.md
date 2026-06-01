# Data Model: Discovery Server Settings Management

## Entity: DiscoveryServerEntry

- Purpose: Represents one persisted discovery server configuration managed in Settings.
- Fields:
  - id: String (stable identifier)
  - hostOrIp: String (normalized hostname or IP address)
  - port: Int (effective port, defaulted to 5959 when omitted at input time)
  - enabled: Boolean
  - orderIndex: Int (persisted user-visible order)
  - createdAtEpochMillis: Long
  - updatedAtEpochMillis: Long
- Validation rules:
  - hostOrIp must be non-empty after trimming.
  - port must be within valid port range.
  - (hostOrIp, port) uniqueness must hold across entries.

## Entity: DiscoveryServerCollection

- Purpose: Represents the complete ordered set of discovery server entries.
- Fields:
  - entries: List<DiscoveryServerEntry>
- Validation rules:
  - orderIndex values must form a stable deterministic order for enabled-entry failover.
  - at least zero entries are allowed.
  - zero enabled entries are allowed; runtime behavior then follows existing fallback behavior.

## Entity: DiscoveryServerDraft

- Purpose: Represents in-progress add/edit form state for the submenu UI.
- Fields:
  - hostInput: String
  - portInput: String
  - resolvedPort: Int (derived, defaults to 5959 when portInput is blank)
  - validationError: String?
  - mode: ADD | EDIT
  - editingEntryId: String?
- Validation rules:
  - hostInput required.
  - non-empty portInput must parse as valid numeric port.
  - save blocked on validation error.

## Entity: DiscoverySelectionResult

- Purpose: Represents runtime selection outcome when discovery usage needs an enabled server.
- Fields:
  - attemptedEntryIds: List<String>
  - selectedEntryId: String?
  - result: SUCCESS | ALL_ENABLED_UNREACHABLE | NO_ENABLED_SERVERS
  - errorMessage: String?
- Validation rules:
  - SUCCESS requires selectedEntryId.
  - ALL_ENABLED_UNREACHABLE requires non-empty attemptedEntryIds and user-visible error message.

## Relationships

- DiscoveryServerCollection 1:N DiscoveryServerEntry.
- DiscoveryServerDraft maps to one DiscoveryServerEntry on successful save.
- DiscoverySelectionResult is derived from DiscoveryServerCollection + connectivity checks at runtime.

## State Transitions

- Draft ADD -> Saved Entry: valid host and port, uniqueness check passed.
- Draft EDIT -> Updated Entry: valid updates, uniqueness preserved.
- Entry enabled true -> false: immediately excluded from sequential failover attempts.
- Entry enabled false -> true: included in sequential failover attempts according to orderIndex.
- Selection SUCCESS: first reachable enabled entry in order selected.
- Selection ALL_ENABLED_UNREACHABLE: all enabled entries attempted and failed; explicit error returned.
