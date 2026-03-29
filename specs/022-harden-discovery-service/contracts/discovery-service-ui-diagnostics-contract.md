# Contract: Discovery Server Validation and Developer Diagnostics

## Scope

Defines externally observable behavior for discovery server add/recheck actions and developer-mode diagnostics visibility.

## Add Server Validation Contract

Input:
- hostOrIp and optional port from discovery server add form.

Required behavior:
- System validates input format.
- System performs discovery protocol-level connection check.
- On success: server is saved and row shows successful check state with timestamp.
- On failure: server remains registered (if save succeeded) and UI shows actionable failure reason.

Non-negotiable success semantic:
- Successful check means NDI discovery-specific handshake/response succeeded for the endpoint.
- TCP reachability alone is insufficient.
- Source presence is not required for success.

## Per-Server Recheck Contract

Input:
- User taps recheck for a specific server row.

Required behavior:
- Only the targeted server is revalidated.
- Recheck updates only that row's check status and timestamp.
- Recheck does not alter server order, enabled flag, or other rows.

Failure behavior:
- Failure result is shown inline with reason and timestamp.
- User can re-trigger recheck after transient failures without re-adding server.

## Developer Mode Diagnostics Contract

Input:
- Developer mode toggle state.

Required behavior:
- When developer mode is ON:
  - UI shows enhanced per-server diagnostics (latest result, timestamp, failure context).
  - UI shows discovery-operation diagnostics and correlated recent logs.
- When developer mode is OFF:
  - Developer-only diagnostics are hidden immediately.
  - Core non-developer UI remains visible and functional.

## Logging and Correlation Contract

Required log events:
- discovery_server_add_started/completed
- discovery_server_check_started/completed
- discovery_server_recheck_started/completed
- discovery_refresh_started/completed

Required correlation behavior:
- Each add/recheck operation carries a correlation id across repository, bridge, and diagnostics logs.
- Failure logs include endpoint identity and failure category.

## Test Assertions

Mandatory assertions in unit and/or e2e tests:
- Add server check success requires protocol response, not socket-only reachability.
- Recheck action updates only the selected row.
- Developer diagnostics visibility strictly follows developer mode toggle.
- Existing discovery list behavior is preserved while new diagnostics are added.
- Failed checks show actionable user feedback and do not crash or clear server registrations.
