# Contract: Discovery Server Settings Management

## 1. Repository Contracts

### 1.1 DiscoveryServerRepository

- observeServers(): stream of ordered DiscoveryServerEntry list
- addServer(hostOrIp: string, portInput?: string): DiscoveryServerEntry
- updateServer(id: string, hostOrIp: string, portInput?: string): DiscoveryServerEntry
- removeServer(id: string): void
- setServerEnabled(id: string, enabled: boolean): DiscoveryServerEntry
- reorderServers(idsInOrder: list<string>): stream of ordered DiscoveryServerEntry list
- resolveActiveDiscoveryTarget(): DiscoverySelectionResult

Behavioral requirements:

- add/update MUST normalize and trim hostOrIp input.
- Blank portInput MUST resolve to 5959.
- Invalid hostOrIp or port MUST be rejected with explicit validation errors.
- Exact duplicates by normalized hostOrIp + effective port MUST be rejected.
- setServerEnabled MUST persist enabled state across app restarts.
- resolveActiveDiscoveryTarget MUST attempt enabled servers in persisted order and fail over sequentially.
- resolveActiveDiscoveryTarget MUST return ALL_ENABLED_UNREACHABLE with actionable error when all enabled servers fail reachability.

### 1.2 NdiSettingsRepository (compatibility behavior)

- Existing settings reads/writes MUST remain backward compatible.
- Any migration from legacy single discovery endpoint to collection model MUST preserve existing configured endpoint as one valid entry.

## 2. ViewModel Contracts

### 2.1 DiscoveryServerSettingsViewModel

Inputs:

- onScreenVisible()
- onAddServerClicked(hostInput, portInput)
- onEditServerClicked(id)
- onSaveEditClicked(id, hostInput, portInput)
- onDeleteServerClicked(id)
- onToggleServerClicked(id, enabled)
- onDismissValidationError()

Outputs (state):

- orderedServers: list of DiscoveryServerEntry view models
- hostInput, portInput, validationError
- isSaveEnabled
- isBusy

Guarantees:

- Save actions are blocked when validation fails.
- Port defaults to 5959 when omitted.
- UI state reflects persisted toggle and list data after restart.

## 3. UI Contracts

### 3.1 Settings and submenu navigation

Required controls:

- Settings entry point for Discovery Servers submenu.
- Submenu list with one row per server showing hostOrIp:port.
- Per-row enabled toggle control.
- Add/edit form with separate hostname-or-ip and port fields.

Required behaviors:

- Hostname-or-ip field required for save.
- Port field optional; blank resolves to 5959.
- Duplicate save attempts show clear error.
- Changes are visible immediately and persist after restart.

## 4. Runtime Selection Contract

- Enabled server selection uses persisted list order.
- On unreachable enabled server, selection MUST proceed to next enabled server.
- If no enabled servers exist, selection result indicates NO_ENABLED_SERVERS and existing fallback behavior is used.
- If all enabled servers are unreachable, selection result indicates ALL_ENABLED_UNREACHABLE and user-visible error guidance is provided.

## 5. Validation Contracts

- Preflight command before emulator e2e: scripts/verify-android-prereqs.ps1.
- Playwright emulator tests MUST cover:
  - opening discovery servers submenu from settings
  - add with host-only input (port defaults to 5959)
  - add with explicit port
  - duplicate prevention feedback
  - per-server toggle persistence after restart
- Existing Playwright regression suite MUST be run and remain passing.
- Validation outputs MUST classify failures as code defects or environment blockers.
