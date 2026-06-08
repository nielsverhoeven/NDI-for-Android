# Technical Plan: NDI Integration Rework — mDNS Fallback, Discovery Server Sourcing, and View/Stream Screen Separation

<!-- Issue: #213 -->
<!-- Branch: feature/213-ndi-integration-rework -->
<!-- Created: 2026-06-07 -->

---

## 1. Architecture Fit

This feature touches four architecture layers (constitution §2.1):

```
View (XAML)              ← AppShell routing, SourceListPage, OutputPage
  ↓
ViewModel                ← SourceListViewModel, OutputViewModel, DiscoverySettingsOrchestrator
  ↓
Repository / Service     ← SourceRepository, SettingsRepository, IDiscoverySettingsOrchestrator
  ↓
NDI Bridge / Platform    ← INdiDiscoveryBridge, NdiDiscoveryBridge, AndroidMulticastLockService
  ↓
SQLite (NdiDatabase)     ← SourceEntity.DiscoveryMode column, stale-source soft-delete
```

The changes are additive at each layer boundary and do not introduce cross-layer shortcuts.
No NDI SDK types will cross the bridge boundary (constitution §2.3).
All UI-thread marshaling continues via `MainThread.BeginInvokeOnMainThread` (constitution §2.3 threading rule).

### Current State (what exists today)

| Concern | Current Behaviour | Problem |
|---|---|---|
| Discovery bridge | `NdiDiscoveryBridge.DiscoverSourcesAsync` returns empty when no host/port is set | No mDNS fallback |
| Orchestrator | `DiscoverySettingsOrchestrator.ApplyAsync` picks only the **first** enabled server via `SetDiscoveryEndpoint` | Multi-server not supported; mDNS never activated |
| Source model | `NdiSource` / `NdiSourceEntry` / `SourceEntity` have no `DiscoveryMode` tag | Sources are untagged |
| Stale sources | No soft-delete on Discovery Server poll misses | Stale entries persist as available |
| Home tab | `//home-tab` → `SourceListPage` discovers sources and offers both viewer and output navigation | Conflates discovery with output launch |
| View tab | `//view-tab` → `ViewerPage` (player only) | Does not show the source list; wrong screen for discovery |
| Stream tab | `//stream-tab` → `OutputPage` receives a `sourceId` query param injected from Home | Output screen depends on a source selection from another screen |

---

## 2. .NET MAUI Implementation Approach

### 2.1 Navigation / Shell Changes

**File: `src/MauiApp/AppShell.xaml`**

The current mapping is:

```
Home   → SourceListPage  (source discovery)
Stream → OutputPage      (output, requires sourceId from Home)
View   → ViewerPage      (player only)
```

After this rework, the mapping becomes:

```
View   → SourceListPage  (source discovery + tap-to-view)
Stream → OutputPage      (self-contained outgoing NDI output)
```

Concrete XAML changes:
- Swap the `ContentTemplate` for the View FlyoutItem / TabBar entry from `ViewerPage` to `SourceListPage`.
- Swap the Home FlyoutItem / TabBar entry content to be retired or repurposed (see §Out of Scope in spec.md — Home tab rework is deferred; the FlyoutItem can remain pointing at `SourceListPage` to preserve the route until a later cleanup task).
- `ViewerPage` remains registered via `Routing.RegisterRoute("viewer", typeof(ViewerPage))` — it is pushed modally from the View screen source list.
- Remove the `output` route registration (or retain it as a no-op) since `OutputPage` is no longer navigated to with a `sourceId` param.

**File: `src/MauiApp/AppShell.xaml.cs`**

- Update `ParseDestination` to correctly classify `//view-rail` / `//view-tab` as `PrimaryNavDestination.View`.
- The Stream / Output / Home / Source route strings already work via substring matching; no change needed there unless route names change.

### 2.2 New Pages, ViewModels, Services

No new page classes are created. The rework modifies existing types in-place. See §4 for the full file-level list.

### 2.3 DI Registration — `src/MauiApp/MauiProgram.cs`

Add:

```csharp
// Android-specific MulticastLock service for mDNS
#if ANDROID
builder.Services.AddSingleton<IMulticastLockService, AndroidMulticastLockService>();
#else
builder.Services.AddSingleton<IMulticastLockService, NoopMulticastLockService>();
#endif
```

No other DI changes are required; all existing singletons/transients remain.

### 2.4 Platform-Specific Code

**New file: `src/MauiApp/Platforms/Android/Services/AndroidMulticastLockService.cs`**

Acquires / releases `WifiManager.MulticastLock` tagged `"ndi_mdns"`.
Called by `NdiDiscoveryBridge` when activating mDNS mode.
`CHANGE_WIFI_MULTICAST_STATE` is already declared in `AndroidManifest.xml` — no manifest change required.

**New file: `src/MauiApp/Services/NoopMulticastLockService.cs`**

No-op implementation for non-Android targets (required so the project builds on all platforms).

---

## 3. NDI Integration

### 3.1 Which NDI SDK Capabilities Are Used

The NDI SDK exposed via `libndi.so` (P/Invoke, `NdiNativeInterop`) provides:

| API | Used for |
|---|---|
| `NDIlib_find_create_v3` with no discovery server address | mDNS source enumeration (standard NDI zero-config path) |
| `NDIlib_find_get_current_sources` | Snapshot of currently visible sources (both modes) |
| `NDIlib_find_destroy` | Cleanup when switching modes or app backgrounding |
| `NDIlib_find_create_v3` with `p_extra.discovery_server_address` set | Discovery Server mode |

If the NDI SDK's built-in mDNS is not exposed through the current P/Invoke surface, a fallback using Android `NsdManager` (`android.net.nsd`) is used instead. The bridge implementation selects between the two at runtime based on `NdiNativeInterop.TryInitialize` success.

### 3.2 Bridge Layer Changes

**`INdiDiscoveryBridge` — interface change** (`src/Core/NdiBridge/INdiBridges.cs`):

Replace:
```csharp
void SetDiscoveryEndpoint(string? host, int? port);
```
With:
```csharp
/// <summary>
/// Sets the active discovery mode. Calling this is idempotent and triggers
/// an immediate internal switch (stopping the previous mode cleanly).
/// </summary>
/// <param name="mode">The discovery mode to activate.</param>
/// <param name="serverEndpoints">
/// Non-empty only when <paramref name="mode"/> is <see cref="DiscoveryMode.DiscoveryServer"/>.
/// All endpoints are queried; results are merged with deduplication.
/// </param>
void SetDiscoveryMode(
    DiscoveryMode mode,
    IReadOnlyList<DiscoveryServerEndpoint>? serverEndpoints = null);
```

Add new model types to `NdiBridgeModels.cs`:
```csharp
public enum DiscoveryMode { Mdns, DiscoveryServer }
public record DiscoveryServerEndpoint(string Host, int Port);
```

Add `DiscoveryMode DiscoveryMode` property to `NdiSourceEntry`.

**`NdiDiscoveryBridge` — implementation changes** (`src/MauiApp/NdiBridge/NdiBridgeImplementations.cs`):

- Replace `_discoveryHost` / `_discoveryPort` fields with `_activeMode` and `_serverEndpoints`.
- `DiscoverSourcesAsync`: dispatch to `DiscoverViaMdnsAsync()` or `DiscoverViaServersAsync()` based on `_activeMode`.
- `DiscoverViaMdnsAsync()`: calls `NDIlib_find_create_v3` with no server address, or Android `NsdManager` if P/Invoke is unavailable; acquires MulticastLock via `IMulticastLockService` before starting, releases on `StopMdnsAsync()`.
- `DiscoverViaServersAsync()`: iterates `_serverEndpoints`, performs TCP reachability check (existing `NetworkReachability.IsTcpReachableAsync`), returns merged source list.
- `SetDiscoveryMode` performs a clean stop of the prior mode before starting the new one. It is thread-safe via a `SemaphoreSlim(1)` guard.
- `NdiSourceEntry` results are tagged with the correct `DiscoveryMode`.

### 3.3 Threading and Lifecycle Constraints

- `SetDiscoveryMode` may be called on any thread (from `DiscoverySettingsOrchestrator.ApplyAsync`). Internal mode switching uses `SemaphoreSlim(1)` and is fully `async`.
- `DiscoverSourcesAsync` is called by `SourceRepository.DiscoverAsync` which is called from `SourceListViewModel.RefreshCommand` on a background thread. Results are published to `Sources` property via standard `ObservableProperty` (MAUI auto-dispatches collection changes to UI thread when using `[ObservableProperty]`).
- `IMulticastLockService.AcquireAsync` / `ReleaseAsync` are called on the background thread inside the bridge; Android's `WifiManager.MulticastLock` is thread-safe for acquire/release.
- View screen lifecycle: MulticastLock is acquired when `SourceListPage.OnAppearing` fires (which triggers `RefreshCommand`) and released when `SourceListPage.OnDisappearing` fires. The ViewModel exposes a `StopDiscoveryCommand` for this.

---

## 4. Data Layer

### 4.1 Schema Change — `SourceEntity`

**File: `src/MauiApp/Data/NdiDatabase.cs`**

Add column to `SourceEntity`:
```csharp
public string DiscoveryMode { get; set; } = "Mdns"; // "Mdns" | "DiscoveryServer"
```

Add migration in `EnsureSettingsColumnsAsync` (existing migration pattern):
```csharp
if (!columnNames.Contains("DiscoveryMode"))
    await _connection.ExecuteAsync(
        "ALTER TABLE sources ADD COLUMN DiscoveryMode TEXT NOT NULL DEFAULT 'Mdns'");
```

### 4.2 Stale-Source Soft-Delete

**New method on `NdiDatabase`:**
```csharp
/// <summary>
/// Sets IsAvailable = false for all Discovery Server sources whose SourceId
/// is NOT in <paramref name="currentSourceIds"/>.
/// mDNS sources are excluded from soft-delete (they use natural expiry).
/// </summary>
public Task MarkDiscoveryServerSourcesStaleAsync(IEnumerable<string> currentSourceIds);
```

Called from `SourceRepository.DiscoverAsync` after a successful Discovery Server poll.

### 4.3 NdiSource Model

**File: `src/Core/Features/Sources/Models/SourceModels.cs`**

Add `DiscoveryMode` property to `NdiSource`:
```csharp
public record NdiSource(
    string SourceId,
    string DisplayName,
    string? EndpointAddress,
    bool IsAvailable,
    long LastSeenAtEpochMillis,
    bool PreviouslyConnected = false,
    NdiBridge.DiscoveryMode DiscoveryMode = NdiBridge.DiscoveryMode.Mdns);  // new
```

> **Constitution note**: `NdiBridge.DiscoveryMode` is a plain C# enum defined in the Core layer — not an NDI SDK type — so crossing into the `Sources` domain model is permitted.

---

## 5. Feature-by-Feature Technical Approach

### 5.1 mDNS Fallback (FR 1–3)

**Trigger:** `DiscoverySettingsOrchestrator.ApplyAsync` is called with a settings snapshot where `DiscoveryServers` is empty or all entries have `Enabled = false`.

**Flow:**
```
ApplyAsync(settings)
  → ResolveMode(settings) returns DiscoveryMode.Mdns
  → bridge.SetDiscoveryMode(Mdns)
      → bridge internally: StopCurrentMode()
      → bridge: multicastLockService.AcquireAsync()
      → bridge: NDIlib_find_create_v3(no server address)   ← mDNS active
```

**`DiscoverSourcesAsync` path (mDNS):**
```
DiscoverViaMdnsAsync()
  → NDIlib_find_get_current_sources()   (or NsdManager.discoverServices)
  → map to NdiSourceEntry[] with DiscoveryMode = Mdns
  → return
```

Continuous refresh is driven by the ViewModel's existing `RefreshCommand` which fires on `OnAppearing` and can be augmented with a periodic timer in the ViewModel (polling interval ≥ 3 s for mDNS, matching the 3 s success criterion in the spec).

**Lifecycle:**
- `SourceListPage.OnAppearing` → `vm.RefreshCommand.Execute()` (already wired)
- `SourceListPage.OnDisappearing` → `vm.StopDiscoveryCommand.Execute()` (new command) → `bridge.SetDiscoveryMode(Mdns)` re-entry is idempotent; MulticastLock is released.

### 5.2 Discovery Server Exclusive Mode (FR 4–10)

**Trigger:** `DiscoverySettingsOrchestrator.ApplyAsync` with ≥1 `Enabled = true` entries.

**Flow:**
```
ApplyAsync(settings)
  → ResolveMode(settings) returns DiscoveryMode.DiscoveryServer
  → endpoints = settings.DiscoveryServers
      .Where(s => s.Enabled)
      .OrderBy(s => s.Order)
      .Select(s => new DiscoveryServerEndpoint(s.Host, s.Port))
  → bridge.SetDiscoveryMode(DiscoveryServer, endpoints)
      → bridge: StopCurrentMode()         ← releases MulticastLock if mDNS was active
      → bridge: stores endpoints, clears NDI find handle
```

**`DiscoverSourcesAsync` path (Discovery Server):**
```
DiscoverViaServersAsync()
  → foreach endpoint in _serverEndpoints:
       reachable = await NetworkReachability.IsTcpReachableAsync(endpoint.Host, endpoint.Port)
       if reachable:
           NDIlib_find_create_v3(discovery_server_address = "host:port")
           sources = NDIlib_find_get_current_sources()
           NDIlib_find_destroy()
           accumulate sources
  → deduplicate by DisplayName, first-server-wins (by Order)
  → tag all with DiscoveryMode = DiscoveryServer
  → return merged list
```

**After successful poll:**
```
SourceRepository.DiscoverAsync()
  → foreach source: db.UpsertSourceAsync(source)
  → db.MarkDiscoveryServerSourcesStaleAsync(currentSourceIds)
```

**Hot mode-switch (FR 9):**
`SettingsRepository.SaveSettingsAsync` already calls `_orchestrator.ApplyAsync(sanitized)` — this path triggers an immediate `SetDiscoveryMode` call on the bridge, satisfying hot-switch without an app restart.

### 5.3 View Screen — Source Discovery and Playback (FR 11, 13)

**Navigation change (AppShell.xaml):**

```xml
<!-- BEFORE -->
<ShellContent Title="View"
              ContentTemplate="{DataTemplate viewer:ViewerPage}"
              Route="view-rail" ... />

<!-- AFTER -->
<ShellContent Title="View"
              ContentTemplate="{DataTemplate sources:SourceListPage}"
              Route="view-rail" ... />
```

Same change applied to the `//view-tab` ShellContent.

**SourceListViewModel changes:**
- Add `string ActiveDiscoveryModeLabel` (`[ObservableProperty]`), populated from `DiscoverySnapshot` or the orchestrator after each `RefreshAsync`.
  - Value: `"mDNS"` when mode is Mdns, `"Discovery Server: host:port"` when DiscoveryServer (showing the first/primary endpoint).
- Add `CancellationTokenSource` for periodic refresh while the screen is active (optional, minimum 3 s interval for mDNS).
- Remove `NavigateToOutputAsync` command — the Stream screen is self-contained; sources are navigated to the viewer only.
- Rename internal clarity: the remaining navigation command is `NavigateToViewerAsync` (already exists).

**How `IDiscoverySettingsOrchestrator` exposes mode to the ViewModel:**
Option: Add `DiscoveryMode ActiveMode { get; }` property to `IDiscoverySettingsOrchestrator`. The `SourceListViewModel` injects `IDiscoverySettingsOrchestrator` and reads `ActiveMode` after each successful `DiscoverAsync` call to build the label. This avoids an extra repository method and keeps the orchestrator as the single source of truth for mode state.

**Discovery mode indicator (FR 13):**
A `Label` in `SourceListPage.xaml` bound to `ActiveDiscoveryModeLabel`. Content description set via `AutomationProperties.Name="{Binding ActiveDiscoveryModeLabel}"` for accessibility (FR: accessibility).

### 5.4 Stream Screen — Outgoing Output Only (FR 12)

**Problem:** `OutputViewModel` currently:
1. Receives `SourceId` as a query parameter from Home tab navigation.
2. Checks reachability of that `SourceId` endpoint before starting output.
3. Calls `INdiOutputBridge.StartOutputAsync(sourceId, streamName)`.

This model is wrong for "originating an NDI output FROM the device" — an NDI sender does not connect TO a remote source; it advertises itself on the network so others can connect.

**Changes to `INdiOutputBridge`** (`src/Core/NdiBridge/INdiBridges.cs`):

Replace:
```csharp
Task StartOutputAsync(string sourceId, string streamName, CancellationToken cancellationToken = default);
```
With:
```csharp
/// <summary>
/// Starts an NDI sender that advertises this device on the network under
/// <paramref name="streamName"/>. No remote sourceId is required.
/// </summary>
Task StartOutputAsync(string streamName, CancellationToken cancellationToken = default);
```

Remove:
```csharp
Task<bool> IsSourceReachableAsync(string sourceId, CancellationToken cancellationToken = default);
```
(Reachability check is only meaningful for Discovery Server, not for a local sender. It will be removed from the interface; `NetworkReachability` stays for internal bridge use.)

**Changes to `OutputViewModel`** (`src/Core/Features/Output/ViewModels/OutputViewModel.cs`):
- Remove `[ObservableProperty] private string? _sourceId`.
- Add `[ObservableProperty] private string _streamName = "NDI-Android"` — user-editable.
- `StartOutputCommand`: calls `bridge.StartOutputAsync(StreamName, ct)` directly; no reachability pre-check.
- Remove `StatusMessage = "Select a source on Home before starting output."` — Stream screen is now self-contained.

**Changes to `OutputPage.xaml.cs`** (`src/MauiApp/Features/Output/Views/OutputPage.xaml.cs`):
- Remove `[QueryProperty(nameof(SourceId), "sourceId")]` decorator.
- Remove `SourceId` property setter.

**Changes to `OutputPage.xaml`** (`src/MauiApp/Features/Output/Views/OutputPage.xaml`):
- Add `Entry` bound to `StreamName` for user-configurable output name.
- Remove any source-related display elements.

**Changes to `AppShell.xaml.cs`**:
- Remove the `output` route registration (`Routing.RegisterRoute("output", typeof(OutputPage))`), or retain it as a safeguard. Since `OutputPage` is no longer pushed via `GoToAsync("output?sourceId=...")`, this route becomes unused.

---

## 6. File-Level Change Plan

The table below lists every file that must be created (`CREATE`) or modified (`MODIFY`). Files are grouped by layer.

### NdiBridge (Core — shared contracts)

| File | Action | Summary |
|---|---|---|
| `src/Core/NdiBridge/NdiBridgeModels.cs` | MODIFY | Add `DiscoveryMode` enum, `DiscoveryServerEndpoint` record; add `DiscoveryMode` field to `NdiSourceEntry` |
| `src/Core/NdiBridge/INdiBridges.cs` | MODIFY | Replace `SetDiscoveryEndpoint` with `SetDiscoveryMode`; simplify `INdiOutputBridge.StartOutputAsync` signature; remove `IsSourceReachableAsync` from interface |

### NdiBridge (MauiApp — platform implementations)

| File | Action | Summary |
|---|---|---|
| `src/MauiApp/NdiBridge/NdiBridgeImplementations.cs` | MODIFY | Implement `SetDiscoveryMode`; add `DiscoverViaMdnsAsync`, `DiscoverViaServersAsync`; update `NdiOutputBridge.StartOutputAsync`; remove `IsSourceReachableAsync` |

### Platform Services

| File | Action | Summary |
|---|---|---|
| `src/Core/Services/IMulticastLockService.cs` | CREATE | Interface: `AcquireAsync(CancellationToken)`, `ReleaseAsync(CancellationToken)` |
| `src/MauiApp/Platforms/Android/Services/AndroidMulticastLockService.cs` | CREATE | `WifiManager.MulticastLock` acquire/release |
| `src/MauiApp/Services/NoopMulticastLockService.cs` | CREATE | No-op implementation for non-Android builds |

### Settings / Orchestration

| File | Action | Summary |
|---|---|---|
| `src/Core/Features/Settings/Services/IDiscoverySettingsOrchestrator.cs` | MODIFY | Add `DiscoveryMode ActiveMode { get; }` property |
| `src/MauiApp/Features/Settings/Services/DiscoverySettingsOrchestrator.cs` | MODIFY | Implement full multi-server support; call `SetDiscoveryMode(Mdns)` when no enabled servers; populate `ActiveMode`; pass all enabled endpoints when in server mode |

### Data Layer

| File | Action | Summary |
|---|---|---|
| `src/MauiApp/Data/NdiDatabase.cs` | MODIFY | Add `DiscoveryMode` column to `SourceEntity`; add `EnsureSettingsColumnsAsync` migration; add `MarkDiscoveryServerSourcesStaleAsync` method |

### Sources Feature

| File | Action | Summary |
|---|---|---|
| `src/Core/Features/Sources/Models/SourceModels.cs` | MODIFY | Add `DiscoveryMode` property to `NdiSource` record |
| `src/Core/Features/Sources/Repositories/ISourceRepository.cs` | MODIFY | Add `Task<NdiBridge.DiscoveryMode> GetActiveDiscoveryModeAsync()` |
| `src/MauiApp/Features/Sources/Repositories/SourceRepository.cs` | MODIFY | Tag each upserted source with `DiscoveryMode`; call stale-source soft-delete after Discovery Server poll; implement `GetActiveDiscoveryModeAsync` |
| `src/Core/Features/Sources/ViewModels/SourceListViewModel.cs` | MODIFY | Inject `IDiscoverySettingsOrchestrator`; add `ActiveDiscoveryModeLabel` property; remove `NavigateToOutputAsync` command; add `StopDiscoveryCommand`; add periodic refresh timer logic |

### Output Feature

| File | Action | Summary |
|---|---|---|
| `src/Core/Features/Output/ViewModels/OutputViewModel.cs` | MODIFY | Remove `SourceId`; add `StreamName`; update `StartOutputCommand` to call `bridge.StartOutputAsync(StreamName)`; remove reachability pre-check; remove "Select a source" message |
| `src/MauiApp/Features/Output/Views/OutputPage.xaml.cs` | MODIFY | Remove `[QueryProperty]` attribute and `SourceId` setter |
| `src/MauiApp/Features/Output/Views/OutputPage.xaml` | MODIFY | Add `Entry` for `StreamName`; remove source-related UI elements |

### Navigation / Shell

| File | Action | Summary |
|---|---|---|
| `src/MauiApp/AppShell.xaml` | MODIFY | Reassign View FlyoutItem and TabBar entry from `ViewerPage` to `SourceListPage`; update route names if needed |
| `src/MauiApp/AppShell.xaml.cs` | MODIFY | Remove `output` route registration (or keep as no-op); verify `ParseDestination` still maps correctly after route reassignment |

### DI Registration

| File | Action | Summary |
|---|---|---|
| `src/MauiApp/MauiProgram.cs` | MODIFY | Register `IMulticastLockService` (Android / noop); inject `IDiscoverySettingsOrchestrator` into `SourceListViewModel` |

### Tests

| File | Action | Summary |
|---|---|---|
| `tests/MauiApp.Tests/Features/Sources/SourceListViewModelTests.cs` | MODIFY | Add orchestrator mock; add tests for `ActiveDiscoveryModeLabel`; remove test for `NavigateToOutputAsync`; add test for `StopDiscoveryCommand` |
| `tests/MauiApp.Tests/Features/Sources/SourceRepositoryTests.cs` | CREATE | Happy/error tests for mDNS mode; happy/error tests for Discovery Server mode; stale-source soft-delete test; `DiscoveryMode` tag verification |
| `tests/MauiApp.Tests/Features/Output/OutputViewModelTests.cs` | MODIFY | Update all tests to remove `SourceId`; add `StreamName` tests; update `StartOutputAsync` mock signature; remove reachability mock setup |
| `tests/MauiApp.Tests/Features/Settings/DiscoverySettingsOrchestratorTests.cs` | CREATE | `ApplyAsync` with no servers → `SetDiscoveryMode(Mdns)`; with servers → `SetDiscoveryMode(DiscoveryServer, endpoints)`; hot-switch test |
| `tests/MauiApp.Tests/NdiBridge/NdiDiscoveryBridgeTests.cs` | CREATE | mDNS mode path; Discovery Server mode with two servers; mode-switch mutual exclusivity; stale/unreachable server returns empty (no throw) |

---

## 7. Testing Strategy

### Unit Test Scope

All tests run via `dotnet test` without requiring `libndi.so` (mock bridge).

| Area | Tests |
|---|---|
| `NdiDiscoveryBridge` | Mode switch, mDNS path, multi-server merge, unreachable server graceful fallback |
| `DiscoverySettingsOrchestrator` | `Mdns` when servers empty; `DiscoveryServer` when ≥1 enabled; hot-switch triggers `SetDiscoveryMode` |
| `SourceRepository` | `DiscoveryMode` tag written; stale soft-delete fires only in server mode; cached sources returned on failure |
| `SourceListViewModel` | `ActiveDiscoveryModeLabel` reflects mode; `StopDiscoveryCommand` releases discovery; `NavigateToOutputAsync` absent |
| `OutputViewModel` | `StartOutputCommand` with valid `StreamName` starts output; empty `StreamName` sets error; no `SourceId` dependency |

### MAUI UI Test Scope

Existing Appium emulator tests (`.github/workflows/emulator-tests.yml`) cover:
- View tab navigation now resolves to source list (not ViewerPage).
- Stream tab contains stream name input and start/stop — no source list visible.

New UI test assertions (in `tests/MauiApp.UITests/`):
- View tab: discovery mode indicator label is present and has a non-empty accessibility description.
- Stream tab: no element with ID matching `SourceListPage` or `SourceId` visible.

### NDI E2E Validation (on-device, T011)

These run only on physical hardware or a physical Android emulator with multicast support:

- mDNS: bring up a NDI source (OBS or NDI Tools) on the same LAN; verify it appears on View tab within 3 s.
- Discovery Server: configure a Discovery Server IP; verify mDNS stops (monitor via tcpdump on a mirrored port); sources from server appear within the polling interval.
- Hot-switch: enable Discovery Server in Settings → Apply; verify mode indicator changes from "mDNS" to "Discovery Server: …".

---

## 8. Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| `libndi.so` does not expose mDNS (no-server `NDIlib_find_create_v3`) via current P/Invoke surface | Medium | High | Fallback: use Android `NsdManager` for `_ndi._tcp.local.` service discovery. Bridge implementation detects at runtime and switches. NDI expert consulted if ambiguous. |
| Removing `IsSourceReachableAsync` from `INdiOutputBridge` breaks an unforeseen caller | Low | Medium | `grep -r "IsSourceReachableAsync"` confirms only `OutputViewModel` and its tests call it. Both are updated in this plan. |
| MulticastLock not released on process kill / crash | Low | Medium | Lock is scoped to View screen lifetime; Android releases WifiManager locks automatically when the process dies. |
| Hot-switch race: `SetDiscoveryMode` called while `DiscoverSourcesAsync` is in-flight | Medium | Medium | `SemaphoreSlim(1)` in `NdiDiscoveryBridge` serialises all mode-change and discovery operations. |
| Removing `NavigateToOutputAsync` from `SourceListViewModel` breaks any XAML binding that references it | Low | Low | Search XAML for `NavigateToOutputCommand` before deletion; replace with nothing (Stream tab is standalone). |
| Removing `[QueryProperty(nameof(SourceId))]` from `OutputPage` while old route `"output?sourceId=..."` is still in the codebase | Low | Medium | Grep for all `GoToAsync` calls that pass `sourceId` to `output`; remove them in the same PR. |

---

## 9. Constitution Compliance

| Constitution Principle | How This Plan Satisfies It |
|---|---|
| **§1 Technology Stack — SQLite-net-pcl async only** | `MarkDiscoveryServerSourcesStaleAsync` and the `DiscoveryMode` column migration both use `_connection.ExecuteAsync` and `InsertOrReplaceAsync` — async throughout. |
| **§2.1 Layering — no DB access from ViewModels** | `SourceListViewModel` reads discovery mode from `IDiscoverySettingsOrchestrator`, not from `NdiDatabase` directly. Source data flows through `ISourceRepository`. |
| **§2.1 Layering — no NDI types cross bridge boundary** | `DiscoveryMode` enum and `DiscoveryServerEndpoint` record are plain C# types defined in the Core layer. No native handles or NDI structs are returned. |
| **§2.1 Layering — no business logic in Views** | Mode switching, stale-source logic, and multi-server deduplication are all in the bridge implementation and orchestrator, not in XAML code-behind. |
| **§2.2 Module Structure** | New `IMulticastLockService` lives in `src/Core/Services/`; its Android implementation in `src/MauiApp/Platforms/Android/Services/`. No new modules are created — all changes fit existing directories. |
| **§2.3 NDI Bridge Pattern — interface in NdiBridge/, mock for tests** | `INdiDiscoveryBridge` interface is in `src/Core/NdiBridge/`; tests use `Mock<INdiDiscoveryBridge>`. No test touches `libndi.so`. |
| **§2.3 NDI Bridge threading rule** | `NdiDiscoveryBridge` marshals results to caller; `SourceListViewModel` already uses `[ObservableProperty]` which dispatches to UI thread. |
| **§2.4 Navigation — MAUI Shell URI routing** | View screen remains a Shell `ShellContent` entry. Viewer playback continues to use `Routing.RegisterRoute("viewer", ...)` push navigation. |
| **§3 Testing Standards — every Repository/ViewModel has Tests.cs** | `SourceRepositoryTests.cs` and `DiscoverySettingsOrchestratorTests.cs` are created by this plan. All modified ViewModels have their existing test files updated. |
| **§3 Testing Standards — min one happy + one error path per public method** | Each new test file includes explicit happy-path and error-path cases (unreachable server, empty source list, cancelled token). |
| **§4 Development Agreements — dotnet build must pass** | No breaking changes to public interfaces are made without updating all call sites in the same plan. Schema migration uses the existing `ALTER TABLE` pattern to avoid destructive changes. |
| **§4 Development Agreements — no secrets in source** | No connection strings, API keys, or Discovery Server addresses are hardcoded; all come from `NdiSettingsSnapshot` which is user-configurable. |
| **§4 Development Agreements — nullable enabled** | All new properties and method parameters carry explicit nullability annotations; `IReadOnlyList<DiscoveryServerEndpoint>?` is nullable with clear semantics (null = mDNS mode). |
| **§5 Android-Specific Rules — manifest permissions** | `CHANGE_WIFI_MULTICAST_STATE` is already declared. No new permissions are required. |
| **§5 Android-Specific Rules — API 26–35 compatibility** | `WifiManager.MulticastLock` is available from API 1; `NsdManager` from API 16. Both are within the API 26–35 window. |
| **§6 CI Rules — dotnet build + unit tests on every PR** | All non-NDI tests remain executable without `libndi.so`. The plan explicitly updates every affected test file. |

---

## 10. Implementation Order

Implement tasks in this sequence to keep the build green after every task (constitution §4.1):

1. **T1** — `NdiBridgeModels.cs`: add `DiscoveryMode`, `DiscoveryServerEndpoint`, update `NdiSourceEntry`.
2. **T2** — `INdiBridges.cs`: update `INdiDiscoveryBridge.SetDiscoveryMode`; update `INdiOutputBridge.StartOutputAsync`.
3. **T3** — `NdiBridgeImplementations.cs`: implement `SetDiscoveryMode`, `DiscoverViaMdnsAsync`, `DiscoverViaServersAsync`; update `NdiOutputBridge`.
4. **T4** — `IMulticastLockService.cs` + `AndroidMulticastLockService.cs` + `NoopMulticastLockService.cs`.
5. **T5** — `IDiscoverySettingsOrchestrator.cs`: add `ActiveMode`; `DiscoverySettingsOrchestrator.cs`: full multi-server + mDNS logic.
6. **T6** — `NdiDatabase.cs`: `DiscoveryMode` column + migration + `MarkDiscoveryServerSourcesStaleAsync`.
7. **T7** — `SourceModels.cs`: add `DiscoveryMode` to `NdiSource`.
8. **T8** — `ISourceRepository.cs` + `SourceRepository.cs`: tagging, stale soft-delete.
9. **T9** — `SourceListViewModel.cs`: `ActiveDiscoveryModeLabel`, `StopDiscoveryCommand`, remove `NavigateToOutputAsync`.
10. **T10** — `OutputViewModel.cs` + `OutputPage.xaml.cs` + `OutputPage.xaml`: remove `SourceId`, add `StreamName`.
11. **T11** — `AppShell.xaml` + `AppShell.xaml.cs`: route reassignment; remove unused `output` route push.
12. **T12** — `MauiProgram.cs`: register `IMulticastLockService`.
13. **T13** — Tests: update `SourceListViewModelTests`, `OutputViewModelTests`; create `SourceRepositoryTests`, `DiscoverySettingsOrchestratorTests`, `NdiDiscoveryBridgeTests`.
14. **T14** — Verify `dotnet build src/MauiApp/NdiForAndroid.csproj -f net10.0-android -c Debug` passes.
15. **T15** — Verify `dotnet test` passes (all non-NDI unit tests).
