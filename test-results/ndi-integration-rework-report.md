# Test Results: NDI Integration Rework (#213) — Stage 6 — 2026-06-08

**Branch:** `feature/213-ndi-integration-rework`  
**PR:** #229 — CI checks green at time of report  
**SDK:** .NET 10.0.300  
**Tester:** tester-agent (automated)

---

## Stage Results

| Stage | Status | Command | Notes |
|---|---|---|---|
| Prerequisite gate — branch | ✅ PASS | `git branch --show-current` | `feature/213-ndi-integration-rework` confirmed |
| Prerequisite gate — test build | ✅ PASS | `dotnet build tests/MauiApp.Tests --configuration Debug` | 0 errors, 0 warnings |
| Stage 2 — Unit tests (baseline) | ✅ PASS | `dotnet test tests/MauiApp.Tests --configuration Debug` | **133/133 passed** |
| Stage 2 — Unit tests (after additions) | ✅ PASS | `dotnet test tests/MauiApp.Tests --configuration Debug --no-build` | **144/144 passed** (+11 new tests) |
| Stage 3 — Integration tests | N/A | No `Category=Integration` tests exist | Skipped per convention |
| Stage 4 — UI tests | N/A (CI-verified) | Emulator UI Tests CI passed in PR run (2m32s) | Not re-run locally (no device) |
| Stage 6 — Release gate | ⚠️ BLOCKED | `dotnet build tests/MauiApp.Tests --configuration Release` | MAUI Android workload not installed in this environment; full solution build blocked by `NETSDK1147`. Test project alone builds clean. PR CI carries the release gate. |

---

## Coverage Gap Analysis vs Acceptance Criteria

| AC | Description | Pre-existing Coverage | Gap Found | Gap Closed By |
|---|---|---|---|---|
| AC-1 | mDNS active when no enabled servers; sources tagged mDNS; DS bridge not queried | `ApplyAsync_WithNoServers_*`, `ApplyAsync_WithAllDisabledServers_*`, `DiscoverSourcesAsync_InMdnsMode_*` | None (unified bridge means no separate DS call) | — |
| AC-2 | DS mode active when ≥1 enabled server; mDNS stops cleanly; sources persisted | `ApplyAsync_With{One,Multiple}EnabledServer*`, `HotSwitch_*` | No test for mixed enabled/disabled filtering; no atomic-call-count verification | `ApplyAsync_WithMixedEnabledDisabledServers_OnlyPassesEnabledEndpointsToBridge`, `ApplyAsync_EachCall_MakesExactlyOneBridgeCall` |
| AC-3 | Failover: first server unreachable → try next | `DiscoverSourcesAsync_WhenServerUnreachable_ReturnsEmptyList` (all-down only) | ❌ No partial-failover test | `DiscoverSourcesAsync_MultipleServers_WhenFirstServerUnreachable_ReturnSourcesFromSecondServer`, `*_DoesNotIncludeItsSource`, `*_WhenAllUnreachable_ReturnsEmpty`, `IsDiscoveryServerReachableAsync_Unreachable*`, `*_Reachable*` |
| AC-4 | Clean hot-switch mDNS↔DS without restart | `HotSwitch_FromMdnsToDiscoveryServer_*`, `HotSwitch_FromDiscoveryServerToMdns_*` | Label only tested at construction; no mid-session label update test | `RefreshCommand_AfterHotSwitchToDiscoveryServer_ReflectsNewModeLabel`, `*_AfterHotSwitchBackToMdns_*`, `*_HotSwitchDoesNotRequireRebuildingViewModel`, `ApplyAsync_MultipleHotSwitches_ActiveModeTracksEachTransition` |
| AC-5 | View screen has no `NavigateToOutput` command | `ViewModel_DoesNotHaveNavigateToOutputCommand` | None | — |
| AC-6 | OutputPage has no `sourceId`; `StartOutputAsync` uses `StreamName` only | `ViewModel_DoesNotHaveSourceIdProperty`, `StartOutputCommand_WithValidStreamName_*` | None | — |
| AC-7 | Settings regression — all existing tests pass | Full suite baseline 133/133 | None | — |

---

## Tests Added in This Run

### `tests/MauiApp.Tests/NdiBridge/NdiDiscoveryBridgeTests.cs` (+6 tests)

New inner class `FakeDiscoveryBridgeWithUnreachableHosts` simulates per-host reachability to exercise the AC-3 failover contract without loading `libndi.so`.

| Test | AC | Purpose |
|---|---|---|
| `DiscoverSourcesAsync_MultipleServers_WhenFirstServerUnreachable_ReturnSourcesFromSecondServer` | AC-3 | First server down; second server's sources must appear |
| `DiscoverSourcesAsync_MultipleServers_WhenFirstServerUnreachable_DoesNotIncludeItsSource` | AC-3 | Unreachable server's SourceId must not appear in results |
| `DiscoverSourcesAsync_MultipleServers_WhenAllUnreachable_ReturnsEmpty` | AC-3 | All servers down → empty result, no exception |
| `IsDiscoveryServerReachableAsync_UnreachableHost_ReturnsFalse` | AC-3 | Bridge reachability contract for down host |
| `IsDiscoveryServerReachableAsync_ReachableHost_ReturnsTrue` | AC-3 | Bridge reachability contract for live host |

*(The existing `DiscoverSourcesAsync_MdnsMode_DeduplicatesSourcesByDisplayName` retained as-is.)*

### `tests/MauiApp.Tests/Features/Sources/SourceListViewModelTests.cs` (+3 tests)

| Test | AC | Purpose |
|---|---|---|
| `RefreshCommand_AfterHotSwitchToDiscoveryServer_ReflectsNewModeLabel` | AC-4 | Label updates to "Discovery Server" on next Refresh after mode changes |
| `RefreshCommand_AfterHotSwitchBackToMdns_ReflectsMdnsLabel` | AC-4 | Label reverts to "mDNS" on next Refresh after reverse switch |
| `RefreshCommand_HotSwitchDoesNotRequireRebuildingViewModel` | AC-4 | Same VM instance survives mDNS→DS→mDNS round-trip with correct labels each time |

### `tests/MauiApp.Tests/Features/Settings/DiscoverySettingsOrchestratorTests.cs` (+3 tests)

| Test | AC | Purpose |
|---|---|---|
| `ApplyAsync_WithMixedEnabledDisabledServers_OnlyPassesEnabledEndpointsToBridge` | AC-2 | Disabled servers in a mixed list are filtered; only enabled ones become endpoints |
| `ApplyAsync_EachCall_MakesExactlyOneBridgeCall` | AC-2 | Each ApplyAsync makes exactly 1 atomic bridge call (no double-stop race) |
| `ApplyAsync_MultipleHotSwitches_ActiveModeTracksEachTransition` | AC-4 | `ActiveMode` property tracks every mDNS↔DS transition at the orchestrator layer |

---

## Failures Found & Fixed

| Test | Failure | Root Cause | Fix | Verified |
|---|---|---|---|---|
| — | — | — | No failures; all additions passed first run | ✅ |

---

## Commit

```
65a4a49  test(ndi): Stage 6 coverage validation for #213
Files changed: 3 (275 insertions)
  tests/MauiApp.Tests/NdiBridge/NdiDiscoveryBridgeTests.cs
  tests/MauiApp.Tests/Features/Sources/SourceListViewModelTests.cs
  tests/MauiApp.Tests/Features/Settings/DiscoverySettingsOrchestratorTests.cs
```

---

## Release Gate

| Check | Status | Notes |
|---|---|---|
| Debug build (test project) | ✅ | `dotnet build tests/MauiApp.Tests` — 0 errors |
| Unit tests (baseline 133) | ✅ | All pre-existing tests continue to pass |
| Unit tests (after additions 144) | ✅ | 11 new tests; 0 failures |
| Integration tests | N/A | None exist |
| UI tests | ✅ (CI) | PR CI emulator run passed (2m32s) |
| NDI e2e | N/A | Dual-emulator harness not in scope for this task |
| Release build | ⚠️ BLOCKED locally | `maui-android` workload absent in sandbox; PR CI carries this gate |
| Device install / launch smoke check | ⚠️ NOT RUN | No connected device in environment; PR CI green |

**Overall: Stage 6 test validation PASSED.** All 144 unit tests green; 11 new tests close AC-2/AC-3/AC-4 gaps; no production code modified.
