# 029 Compatibility Matrix Evidence

## Validation Metadata
- Feature: 029-ndi-server-compatibility
- Date: 2026-04-07
- Status: blocked-awaiting-runtime-targets

## Target Results

| targetId | role | versionString | discoveryStatus | streamStartStatus | compatibilityStatus | failureCategory | evidenceRef | notes |
|---|---|---|---|---|---|---|---|---|
| baseline-latest | baseline | UNKNOWN_PENDING_CAPTURE | blocked | blocked | blocked | environment | test-results/029-us1-venue-discovery.md | Endpoint host and concrete version string are required to run version-specific validation |
| venue-failing | venue | UNKNOWN_PENDING_CAPTURE | blocked | blocked | blocked | environment | test-results/029-us1-venue-discovery.md | Venue endpoint/version capture pending before compatibility execution |

## Blocked Targets

| targetId | blocker | unblockStep | recordedAt |
|---|---|---|---|
| baseline-latest | Baseline endpoint/version not yet captured | Capture host/version, verify reachability, then run discovery + stream-start validation | 2026-04-07 |
| venue-failing | Venue endpoint/version not yet captured | Capture host/version, verify reachability from venue network, then run discovery + stream-start validation | 2026-04-07 |

## Implemented Behavior Validation (Code-Level)
- Mixed configured endpoints do not collapse to full success when any endpoint is non-compatible.
- Compatibility outcomes are propagated to Source List state and partial-compatibility telemetry is emitted.
- Evidence: `test-results/029-us1-venue-discovery.md`, `test-results/029-us1-test-change-traceability.md`.

## NDI Documentation Notes Applied
- `https://docs.ndi.video/all/getting-started/white-paper/discovery-and-registration`: discovery may use mDNS or Discovery Service; environment/network topology affects discoverability.
- `https://docs.ndi.video/all/developing-with-ndi/sdk/ndi-find`: discovery can require several seconds and early source reads can be incomplete; segmented networks may block mDNS traffic.

## Classification Rules Applied
- compatible: discovery and stream start succeed
- limited: discovery works but validated support is discovery-only
- incompatible: stream start fails in an otherwise ready environment
- blocked: validation cannot complete due to environment or endpoint blocker

## Command Evidence
- See `test-results/029-preflight-android-prereqs.md`
- See `test-results/029-preflight-adb-devices.md`
- See `test-results/029-preflight-node-playwright.md`

## US2 Matrix Automation Run (2026-04-07)
- Command: `pwsh -NoProfile -ExecutionPolicy Bypass -File .\testing\e2e\scripts\run-discovery-compatibility-matrix.ps1 -Profiles pr-primary`
- Result: PASS (54 passed)
- Note: The automation validated compatibility-related diagnostics/contracts and matrix harness behavior. Runtime per-version discovery/stream validation for baseline-latest and venue-failing remains blocked until endpoint/version capture is provided.
