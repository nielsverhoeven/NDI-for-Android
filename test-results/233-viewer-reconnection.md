# Test Results: Automatic Viewer Reconnection with Retry Window — Issue #233 / PR #260

**Branch:** `feature/233-automatic-viewer-reconnection-retry`
**Date:** 2026-06-04
**Feature:** Reconnection state machine in `ViewerViewModel` (Core), `ConnectionState` enum +
`GetConnectionState()` on `INdiViewerBridge`, `IMainThreadDispatcher` abstraction, ViewerPage UI bindings.

---

## Stage Results

| Stage | Status | Command | Notes |
|---|---|---|---|
| 1 — Build Validation | ✅ | `dotnet build NdiForAndroid.sln` | Build succeeded, 0 errors, 14 warnings (pre-existing MAUI/obsolete-API/XA4301 warnings, none from this feature). Elapsed ~2m36s. |
| 2 — Unit Tests | ✅ | `dotnet test tests/MauiApp.Tests` | **153 passed, 0 failed, 0 skipped.** Duration ~1s. |
| 3 — Integration Tests | n/a | — | No integration-tagged tests for this feature; state machine is exercised via unit tests with `FakeTimeProvider` + synchronous fake dispatcher. |
| 4 — MAUI UI Tests | n/a | — | UI surface is XAML bindings only (no business logic in code-behind, per plan §3). No new UI test scenarios required. |
| 5 — NDI E2E (dual-emulator) | n/a (documented) | — | See "Android device validation rationale" below. |
| 6 — Release Gate | not run | — | Not requested; CI (PR #260) Release/Emulator jobs already green. |

---

## Test Coverage Review (acceptance criteria → tests)

Reviewed `tests/MauiApp.Tests/Features/Viewer/ViewerViewModelTests.cs` against issue #233 acceptance
criteria and the plan's testing strategy (§2.3 mapping). All nine required behaviors are covered and
passing — **no tests needed to be generated or healed.**

| # | Required behavior | Test method | Status |
|---|---|---|---|
| 1 | Unexpected drop starts the 15s retry window | `Drop_WhilePlaying_EntersReconnecting` | ✅ |
| 2 | Retry attempts on ~2s cadence (StopReceiver→StartReceiver per attempt) | `RetryWindow_RunsAttemptsEvery2s` | ✅ |
| 3 | Countdown text updates ("Reconnecting... {n}s remaining") | `Countdown_DecrementsEachSecond` | ✅ |
| 4 | First successful Connected stops retrying (CompleteReconnect) | `Reconnect_Succeeds_ResumesPlayback` | ✅ |
| 5 | Window expiry → terminal "Connection lost. Reconnection failed." + CanReconnect true | `Expiry_NoReconnect_SetsErrorState` | ✅ |
| 6 | CancelRetryCommand stops retrying and disposes timers | `CancelRetry_DuringWindow_StopsAndClears` | ✅ |
| 7 | User-initiated Stop does NOT trigger auto-retry | `Stop_ByUser_DoesNotTriggerRetry` | ✅ |
| 8 | ReconnectCommand restarts with last SourceId | `Reconnect_FromErrorState_RestartsFlow` | ✅ |
| 9 | Drop during initial connect handled | `Drop_DuringInitialConnect_EntersReconnecting` | ✅ |

Plus 3 ctor regression tests (`StartCommand_*`, `StopCommand_*`) confirming the new constructor
signature (`INdiViewerBridge`, `TimeProvider`, `IMainThreadDispatcher`) integrates cleanly.

Note on behavior #6: timer-disposal is asserted indirectly and correctly — after `CancelRetry`, the
test advances the fake clock 10s and verifies `StartReceiver` is never invoked again, proving the
active retry timer was disposed (no overlapping/leaked loop), per plan NFR5.

### Tests Added / Healed
None. Coverage was complete and all assertions green. No production code was touched.

---

## Android Device Validation — Documented Rationale (not a skipped stage)

Per the orchestrator gate, device-facing behavior normally requires `/android-build-install-run`
evidence. For this feature that on-device step is **not applicable** and is an accepted documented
blocker, because:

- The NDI viewer bridge is a **deterministic stub** on this branch (no real libndi receive loop;
  wiring the P/Invoke loop is explicitly out of scope per spec). `GetConnectionState()` is a stubbed
  getter, so there is **no new device-observable runtime behavior** to validate.
- The entire testable surface is the reconnection **state machine** in `ViewerViewModel` (Core),
  which is fully exercised deterministically via `FakeTimeProvider` + a synchronous fake
  `IMainThreadDispatcher` (no wall-clock, no hardware) — matching the issue's testability gate (AC6).
- The ViewerPage changes are XAML bindings only with no business logic in code-behind.
- CI (PR #260) is **all green including Emulator Tests**, confirming the build installs and launches
  cleanly on an emulator.

---

## Release Gate Summary

| Check | Status |
|---|---|
| Debug build | ✅ |
| Unit tests (153) | ✅ |
| Integration tests | n/a (no integration suite for feature) |
| UI tests | n/a (bindings-only) |
| NDI e2e | n/a — stubbed bridge, no on-device observable behavior (documented above); CI Emulator Tests green |
| Release build | not run (CI green on PR #260) |
| Device install / launch smoke check | n/a — covered by CI Emulator Tests (documented above) |

**Verdict:** ✅ Feature validated. Build green, all 153 unit tests pass, and all nine required
reconnection behaviors have deterministic coverage. No tests added/healed; no production code changed.
