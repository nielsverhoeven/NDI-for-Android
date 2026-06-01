# Contract: NDI Discovery Routing Reliability

## 1. Discovery Mode Selection Contract

- Discovery mode MUST be selected once at the beginning of each run using the current enabled discovery-server configuration.
- If enabled discovery server count is `0`, run mode MUST be multicast/mDNS.
- If enabled discovery server count is `>= 1`, run mode MUST be discovery-server-only and mDNS MUST NOT execute in that run.
- Mode selection reason and selected mode MUST be diagnostic-observable in run output.

## 2. Discovery-Server Query Contract

- Discovery-server mode MUST query discovery servers for source records only.
- Returned source endpoint host/port values MUST be used as source endpoint data.
- Discovery-server endpoint values MUST NOT be treated as stream endpoints.
- Invalid source records (missing/invalid host/port) MUST be excluded and logged with identifiable diagnostics.

## 3. Timeout and Failure Contract

- Discovery-server mode MUST complete successfully within 5000ms or return a timeout result.
- Timeout result MUST include explicit timeout reason and run context.
- Discovery-server timeout/failure MUST NOT silently fall back to multicast within the same run.
- Typical healthy completion target is 2000ms and is measured as a performance objective, not a fallback trigger.

## 4. Cache Persistence and Merge Contract

- Every usable discovered source MUST be persisted to app cache storage.
- Rediscovered sources MUST update existing canonical cached rows rather than create duplicates.
- Canonical identity conflicts (same identity, different endpoint) MUST update endpoint fields on the existing row with newest discovery timestamp.
- Cache updates MUST preserve retained continuity fields (including preview-path metadata) unless explicitly replaced by valid new values.

## 5. Relaunch Visibility Contract

- When cached rows exist, app relaunch/update flows MUST emit cached sources before live discovery completes.
- Discovery timeout/failure MUST preserve existing cached rows.
- Availability/validation state changes MUST not remove canonical cached identity rows unexpectedly.

## 6. Diagnostics and Blocker Classification Contract

- Discovery-server mode MUST emit per-server timing and status diagnostics sufficient to identify slow or unreachable servers.
- Validation reporting MUST classify failures as code failures or environment blockers with reproduction details.
- Environment-blocked runs MUST include endpoint/network fixture evidence and timestamps.

## 7. Test Coverage Contract

- JUnit tests MUST be written/updated first for:
  - mode selection (`0` enabled servers vs `>=1` enabled servers)
  - 5-second timeout and no same-run fallback behavior
  - canonical identity merge updates with endpoint conflict handling
  - per-server diagnostics capture and blocker classification mapping
- Playwright e2e coverage MUST include:
  - multicast path with no enabled discovery servers
  - discovery-server-only path with mDNS disabled
  - cache visibility after relaunch with preserved app data
- Existing Playwright regression suites MUST be executed and remain passing.
- Required preflights before e2e gates:
  - `pwsh ./scripts/verify-android-prereqs.ps1`
  - `adb devices`
  - dual-emulator preflight scripts when dual-emulator scenarios are used
