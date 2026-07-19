# NDI-for-Android — Agent Knowledge Base
<!-- Last updated: 2026-07-07 | Read this INSTEAD of re-reading constitution.md + architecture.md for implementation tasks -->

## Tech Stack (authoritative)
- **Platform**: .NET MAUI `net10.0-android` | **Language**: C# 12, nullable enabled
- **UI**: MAUI Shell + XAML; SkiaSharp for the NDI video surface | **MVVM**: CommunityToolkit.Mvvm (`ObservableObject`, `[RelayCommand]`, `[ObservableProperty]`)
- **DI**: `Microsoft.Extensions.DependencyInjection` via `MauiProgram.cs` — no service locator
- **Persistence**: SQLite-net-pcl (async API only) | **NDI**: **real P/Invoke** against bundled `libndi.so` (NDI SDK 6.3.1, arm64-v8a + armeabi-v7a; soft-disabled on x86/x86_64 — no native lib there)
- **Tests**: xUnit 2.x + Moq | **Build**: `dotnet CLI`
- **CI**: GitHub Actions — `.github/workflows/emulator-tests.yml`, `.github/workflows/release.yml`

## Build & Test Commands
```powershell
dotnet build src/NdiForAndroid.sln          # Must pass after every task
dotnet test tests/MauiApp.Tests             # Non-NDI unit tests — must pass before PR merge
```

## Key File Paths
| Purpose | Path |
|---------|------|
| DI root | `src/MauiApp/MauiProgram.cs` |
| Shell routing | `src/MauiApp/AppShell.xaml.cs` |
| Settings ViewModel | `src/Core/Features/Settings/ViewModels/SettingsViewModel.cs` |
| Settings Models | `src/Core/Features/Settings/Models/SettingsModels.cs` |
| Settings Repository interface | `src/Core/Features/Settings/Repositories/ISettingsRepository.cs` |
| Settings Repository impl | `src/MauiApp/Features/Settings/Repositories/SettingsRepository.cs` |
| Settings View | `src/MauiApp/Features/Settings/Views/SettingsPage.xaml(.cs)` |
| NDI bridge interfaces + models | `src/Core/NdiBridge/` (`INdiBridges.cs`, `NdiBridgeModels.cs`, `QualityProfile.cs`) |
| NDI runtime lifecycle (init/config/version) | `src/MauiApp/NdiBridge/NdiRuntime.cs` |
| NDI P/Invoke surface | `src/MauiApp/NdiBridge/Interop/NdiNativeMethods.cs` + `NdiNativeStructs.cs` |
| NDI discovery bridge | `src/MauiApp/NdiBridge/NdiDiscoveryBridge.cs` |
| NDI viewer bridge (recv, tally, PTZ, stats) | `src/MauiApp/NdiBridge/NdiViewerBridge.cs` |
| NDI output bridge (send + re-stream) | `src/MauiApp/NdiBridge/NdiOutputBridge.cs` |
| Capture source contracts | `src/Core/Services/ICaptureSources.cs`, `IAudioPlaybackSink.cs`, `INdiPlatformBootstrap.cs` |
| Android capture/audio/NSD services | `src/MauiApp/Platforms/Android/Services/` (`AndroidVideoCaptureSource`, `AndroidMicrophoneCaptureSource`, `AndroidAudioPlaybackSink`, `AndroidNsdBootstrap`, `ScreenShareForegroundService`) |
| Reusable NDI render surface | `src/MauiApp/Features/Viewer/Views/ViewerView.xaml(.cs)` (SkiaSharp, ~30 fps pull loop) |
| Window size class + nav policy | `src/Core/Features/Navigation/Services/` (`WindowSizeClassService`, `NavigationPolicyService`) |
| SQLite/Data layer | `src/MauiApp/Data/` |
| Android platform services | `src/MauiApp/Platforms/Android/` |
| Unit tests | `tests/MauiApp.Tests/` |
| NDI SDK coverage matrix | `docs/ndi-sdk-coverage.md` |
| Constitution (full detail) | `docs/constitution.md` |
| Architecture (full detail) | `docs/architecture.md` |

## Module Structure
```
src/
  Core/                   ← Domain contracts (interfaces, models, ViewModels)
    Features/{Feature}/
      Models/
      Repositories/       ← Interfaces only
      Services/           ← Interfaces only
      ViewModels/
  MauiApp/                ← App composition + implementations
    Features/{Feature}/
      Repositories/       ← Concrete SQLite/NDI implementations
      Services/
      Views/              ← XAML + code-behind
    NdiBridge/            ← P/Invoke implementations (only layer doing native interop)
    Data/                 ← SQLite context
    Platforms/Android/    ← Android-only lifecycle, permissions, MediaProjection
tests/
  MauiApp.Tests/          ← xUnit, no native NDI dependency (use mock bridge)
  MauiApp.UITests/        ← Appium UI smoke tests
```

## Architecture Rules (must not violate)
1. **No direct DB access from ViewModels** — always via repository interface
2. **No NDI types cross bridge boundary** — bridge returns plain C# records only
3. **No business logic in Views** — Views are pure XAML + bindings
4. **NDI threading**: bridge events (`ConnectionStateChanged`, `TallyEchoChanged`, `OutputStatusChanged`) are raised on pump/background threads — subscribers marshal to the UI thread (`IMainThreadDispatcher` in Core, `MainThread.BeginInvokeOnMainThread` in MauiApp)
5. **Android APIs** isolated in `Platforms/Android/` behind interfaces

## Shell Routes

Bottom TabBar placement:

| Route | Page | Purpose |
|---|---|---|
| `//home-tab` | `HomePage` | Home dashboard — discovery/viewer/output status |
| `//stream-tab` | `OutputPage` | Stream tab — outgoing NDI output only; no `sourceId` param |
| `//view-tab` | `SourceListPage` | View tab — discovery + tap-to-view; two-pane (embedded `ViewerView`) on Expanded windows |
| `//settings-tab` | `SettingsPage` | Settings |
| `viewer?sourceId={id}` | `ViewerPage` | Pushed relative to current tab; registered via `Routing.RegisterRoute("viewer", typeof(ViewerPage))` in `AppShell.xaml.cs` |
| `diagnostic-log` | `DiagnosticLogPage` | Pushed; registered in `AppShell.xaml.cs` |

Left navigation rail placement: same pages on `//home-rail`, `//stream-rail`, `//view-rail`, `//settings-rail`.

**Placement policy (#279)**: `WindowSizeClass` = Compact (<600dp) / Medium (600–840dp) / Expanded (>840dp), fed from `AppShell.OnSizeAllocated`. `NavigationPolicyService`: **rail when landscape OR Expanded, bottom tabs otherwise**. `AppShell` selects `-rail` vs `-tab` routes from `AdaptiveShellStateViewModel.IsLeftRailNavigationVisible`.

## Settings Feature (Issue #142 — MERGED to main, PR #211)
- **ViewModel**: `SettingsViewModel` — 5 sections: General, Appearance, Discovery, DeveloperTools, About
- **Model**: `NdiSettingsSnapshot` — holds all persisted settings as immutable record
- **Sections**: `SettingsSection` enum drives `IsXxxSectionSelected` properties
- **Auto-save (#292)**: no Apply button — every change (theme/accent/dev-mode, discovery-server add/edit/delete/toggle/reorder) persists immediately via `PersistAsync()` → `ISettingsRepository.SaveSettingsAsync`
- **Discovery servers (#292)**: three add fields (optional display name, hostname, port); rows show display name (hostname fallback), endpoint, and live connection state (`DiscoveryServerConnectionState`, 10 s TCP probe via `INdiDiscoveryBridge.IsDiscoveryServerReachableAsync`, started/stopped from page `OnAppearing`/`OnDisappearing`); editing opens an in-page modal overlay (scrim + dialog) driven by `IsEditServerDialogOpen`. Legacy single `DiscoveryHost`/`DiscoveryPort` fields removed (columns remain unmapped in SQLite).
- **Platform info**: `ISettingsPlatformService.GetAppInfo()` → `SettingsAppInfo(AppName, Version, Build)`
- **Cached sources**: loaded from `ISourceRepository.GetCachedSourcesAsync()`
- **Appearance service**: `IAppearanceService` / `MauiAppearanceService` — central runtime color application
  - `DarkPalette` / `LightPalette` records hold all 16 semantic color values
  - `UpdateResources()` writes to `Application.Current.Resources` (DynamicResource triggers)
  - `UpdateShell()` sets Shell tab bar, title, foreground via `SetValue()`
  - `UpdateAndroidStatusBar()` — `#if ANDROID` guard — sets status bar color via `WindowCompat`
- **Color system**: `Colors.xaml` defines 16 semantic keys (e.g. `PageBackground`, `ShellBackground`, `Primary`). ALL elements must use `DynamicResource` — never `StaticResource` or hardcoded hex.
- **RadioButton**: uses pure MAUI `ControlTemplate` (two `Ellipse` elements) — native Android `MaterialRadioButton` ignores `DynamicResource`.

## NDI Bridge — Real P/Invoke Implementation (#277 receive / #278 send, MERGED)

The placeholder `NdiBridgeImplementations.cs` is **GONE** — the bridge is a real integration
against the bundled NDI SDK 6.3.1. Full per-capability status: `docs/ndi-sdk-coverage.md`.

- **File split** (`src/MauiApp/NdiBridge/`): `NdiRuntime.cs` (lifecycle/config/version), `Interop/NdiNativeMethods.cs` + `NdiNativeStructs.cs` (all `[DllImport("ndi")]`), `NdiDiscoveryBridge.cs`, `NdiViewerBridge.cs`, `NdiOutputBridge.cs`, `NetworkReachability.cs`.
- **Init**: everything goes through `NdiRuntime.EnsureInitialized()` (pair with `ReleaseHandle()`). It calls `INdiPlatformBootstrap.EnsureReady()` first — Android **must** hold `NsdManager` before any NDI object (`AndroidNsdBootstrap`). Returns `false` (never throws) on x86 emulators / non-NEON CPUs → NDI features soft-disable.
- **Discovery-server config is a FILE, not an API**: `NdiRuntime` writes `ndi-config.v1.json` (`ndi.networks.discovery = "host:port,host2:port2"`) into app data and sets `NDI_CONFIG_DIR` **before** `NDIlib_initialize`. The lib reads config once — changes reinitialize when idle or defer until the last active handle drops. Server hosts are also passed as `p_extra_ips` on the finder.
- **Discovery**: one long-lived `NDIlib_find_create_v2` finder per mode config (the finder accumulates sources across polls); MulticastLock held for mDNS polls.
- **Receive** (`NdiViewerBridge`): `recv_create_v3` (BGRX/BGRA) + two pump threads (video+metadata, audio); latest-frame double buffer (no copy on UI poll); 3 s stall watchdog + `recv_get_no_connections` drive `ConnectionStateChanged`; tally both directions (`recv_set_tally` re-applied per reconnect + `ndi_tally_echo` metadata); PTZ gated on `ptz_is_supported` (re-checked on StatusChange frames); real FPS/drop stats. `QualityProfile.Smooth` → `bandwidth_lowest`, else `highest` (recreate receiver on change — bandwidth is create-time).
- **Every captured frame MUST be freed** (`recv_free_*`) — leak = native OOM in seconds. Pump threads swallow exceptions (background-thread crash kills the process).
- **Send** (`NdiOutputBridge`): `StartOutputAsync(streamName, VideoInputKind, captureMicrophone)` — screen (MediaProjection), front/rear camera (Camera2 → NV12), mic (AudioRecord float PCM via `util_send_send_audio_interleaved_32f`). Video sent **synchronously** from the capture callback (producer owns/reuses the buffer). 1 s poll of `send_get_tally`/`send_get_no_connections` → `OutputStatusChanged`. Zero-copy re-stream: dedicated recv→send pump (`StartReStreamFromSourceAsync`).
- **Audio playback**: `IAudioPlaybackSink` → `AndroidAudioPlaybackSink` (AudioTrack float PCM); FLTP planar → interleaved in the audio pump.
- Sending runs under `ScreenShareForegroundService` (`foregroundServiceType="mediaProjection|camera|microphone"`; on API 34+ only currently-granted types are passed to `StartForeground` or it throws `SecurityException`).

## NDI Integration Rework (Issue #213 — **MERGED**, PR #229, branch: feature/213-ndi-integration-rework)

> Historical record. Bridge file names/signatures below were superseded by #277/#278 (see section above).

Feature plan: `docs/features/ndi-integration-rework/plan.md`
Release notes: `docs/features/ndi-integration-rework/release-notes.md`

### Key design decisions
| Concern | Decision |
|---------|----------|
| Discovery mode selection | `DiscoveryMode` enum (`Mdns` \| `DiscoveryServer`) in `src/Core/NdiBridge/NdiBridgeModels.cs`; activated via `INdiDiscoveryBridge.SetDiscoveryMode()` |
| mDNS activation | `NDIlib_find_create_v3` with no server address; `IMulticastLockService` acquired before each mDNS poll, released on switch to Discovery Server mode |
| MulticastLock | `IMulticastLockService` / `AndroidMulticastLockService` — acquired on mDNS start, released on mode switch; `NoopMulticastLockService` on non-Android targets |
| Discovery Server mode | Multi-server; TCP reachability check (2-second timeout) per server; results merged + deduplicated by `DisplayName` |
| Stale source cleanup | `NdiDatabase.MarkDiscoveryServerSourcesStaleAsync()` soft-deletes DS sources not in latest poll; called by `SourceRepository.DiscoverAsync` after every DS poll |
| Bridge API | `SetDiscoveryEndpoint(host, port)` replaced by `SetDiscoveryMode(mode, endpoints)` guarded by `SemaphoreSlim(1)` |
| Schema migration | `DiscoveryMode TEXT NOT NULL DEFAULT 'Mdns'` added to `sources` table via `ALTER TABLE` in `NdiDatabase.EnsureSourceColumnsAsync()` — additive, safe for existing installs |
| Shell nav | Home and View tabs → `SourceListPage`; Stream tab → self-contained `OutputPage` (no sourceId param) |
| `NdiSource` model | `DiscoveryMode` property (`DiscoveryMode` enum — crosses bridge boundary as plain C# type, permitted by constitution) |
| `SourceListViewModel` | `ActiveDiscoveryModeLabel` (observable string), `StopDiscoveryCommand` (calls `IDiscoveryRefreshService.Stop()`), `NavigateToViewerAsync` — no `NavigateToOutputAsync`; constructor requires `IDiscoveryRefreshService` 4th arg; **Singleton** lifetime |
| `OutputViewModel` | `StreamName` (observable, default `"NDI-Android"`) drives output; `StartOutputAsync(CancellationToken)` — no source selection required; no `SourceId` |
| `IDiscoverySettingsOrchestrator` | `ActiveMode` property reflects current mode after `ApplyAsync`; read by `SourceRepository` and `SourceListViewModel` |

### DI registrations added in `MauiProgram.cs`
```csharp
// Platform-conditional multicast lock
#if ANDROID
builder.Services.AddSingleton<IMulticastLockService, AndroidMulticastLockService>();
#else
builder.Services.AddSingleton<IMulticastLockService, NoopMulticastLockService>();
#endif

// Orchestrator (singleton, already registered — verify before adding again)
builder.Services.AddSingleton<IDiscoverySettingsOrchestrator, DiscoverySettingsOrchestrator>();

// Auto-refresh polling service (#232)
builder.Services.AddSingleton<IDiscoveryRefreshService, DiscoveryRefreshService>();
// SourceListViewModel + SourceListPage are Singleton (subscribes to singleton refresh service)
builder.Services.AddSingleton<SourceListViewModel>();
builder.Services.AddSingleton<Features.Sources.Views.SourceListPage>();
```

### New/changed files for #213
- `src/Core/NdiBridge/INdiBridges.cs` — `SetDiscoveryMode()` replaces `SetDiscoveryEndpoint()`; `INdiOutputBridge.StartOutputAsync(streamName)` — no sourceId
- `src/Core/NdiBridge/NdiBridgeModels.cs` — `DiscoveryMode` enum, `DiscoveryServerEndpoint` record, `DiscoveryMode` on `NdiSourceEntry`
- `src/Core/Features/Sources/Models/SourceModels.cs` — `DiscoveryMode` property on `NdiSource`
- `src/Core/Features/Sources/Repositories/ISourceRepository.cs` — `GetActiveDiscoveryModeAsync()` added
- `src/Core/Features/Settings/Services/IDiscoverySettingsOrchestrator.cs` — `ActiveMode` property added
- `src/Core/Features/Settings/Services/DiscoverySettingsOrchestrator.cs` — `ApplyAsync` sets mode on bridge; `ActiveMode` tracked
- `src/MauiApp/NdiBridge/NdiBridgeImplementations.cs` — dual-mode `NdiDiscoveryBridge`; `NdiOutputBridge.StartOutputAsync(streamName)`
- `src/MauiApp/Data/NdiDatabase.cs` — `DiscoveryMode` column migration + `MarkDiscoveryServerSourcesStaleAsync()`
- `src/MauiApp/Features/Sources/Repositories/SourceRepository.cs` — mode tagging + stale soft-delete after DS poll
- `src/Core/Features/Sources/ViewModels/SourceListViewModel.cs` — `ActiveDiscoveryModeLabel`, `StopDiscoveryCommand`, `NavigateToViewerAsync`
- `src/Core/Features/Output/ViewModels/OutputViewModel.cs` — `StreamName` drives output; no source selection
- `src/MauiApp/Platforms/Android/Services/AndroidMulticastLockService.cs` — NEW
- `src/MauiApp/Services/NoopMulticastLockService.cs` — NEW
- `src/Core/Services/IMulticastLockService.cs` — NEW
- `src/MauiApp/MauiProgram.cs` — registers `IMulticastLockService` conditionally
- `src/MauiApp/AppShell.xaml` — View tab → `SourceListPage`; Stream tab → `OutputPage`

### New/changed files for #232 (auto-refresh — PR #242)
- `src/Core/Services/IDiscoveryRefreshService.cs` — NEW: polling service interface (`Start`, `Stop`, `RequestRefresh`, `SnapshotReady` event)
- `src/Core/Services/DiscoveryRefreshService.cs` — NEW: 5s polling loop; `Interlocked.CompareExchange` guards; `TimeProvider` injected; subscribes to `IAppLifecycleService.AppResumed/AppPaused`
- `src/Core/Services/IAppLifecycleService.cs` — `AppResumed` and `AppPaused` events added
- `src/Core/Services/AppLifecycleService.cs` — MOVED from `src/MauiApp/Services/` (no platform deps → Core per rule 7); fires new events
- `src/Core/Features/Sources/ViewModels/SourceListViewModel.cs` — injects `IDiscoveryRefreshService` (4th ctor param); subscribes to `SnapshotReady`; `RefreshCommand` delegates to `RequestRefresh()`; **Singleton** lifetime
- `src/MauiApp/MauiProgram.cs` — registers `IDiscoveryRefreshService` Singleton; `SourceListViewModel` + `SourceListPage` promoted to Singleton
- `src/Core/NdiForAndroid.Core.csproj` — adds `Microsoft.Extensions.Logging.Abstractions`
- `tests/MauiApp.Tests/Services/DiscoveryRefreshServiceTests.cs` — NEW: 11 unit tests
- `tests/MauiApp.Tests/Services/AppLifecycleServiceTests.cs` — NEW: 4 event tests
- `tests/MauiApp.Tests/Features/Sources/SourceListViewModelTests.cs` — rewritten to use `SnapshotReady` event-raising pattern (Moq `Raise`)

### ViewModel (CommunityToolkit)
```csharp
public partial class MyViewModel : ObservableObject
{
    [ObservableProperty] private string _myProp = string.Empty;
    [RelayCommand] private async Task DoThingAsync() { ... }
    [RelayCommand(CanExecute = nameof(CanDoThing))] private void DoThing() { ... }
    private bool CanDoThing() => !string.IsNullOrEmpty(MyProp);
}
```

### Repository interface pattern
```csharp
// In src/Core/Features/X/Repositories/IXRepository.cs
public interface IXRepository
{
    Task<XModel> GetAsync();
    Task SaveAsync(XModel model);
}
```

### DI registration (MauiProgram.cs)
```csharp
builder.Services.AddSingleton<IXRepository, XRepository>();
builder.Services.AddTransient<XViewModel>();
```

## Automatic Viewer Reconnection (Issue #233 — PR #260, branch: feature/233-automatic-viewer-reconnection-retry)

Feature spec: `docs/features/automatic-viewer-reconnection-retry/spec.md`
Release notes: `docs/features/automatic-viewer-reconnection-retry/release-notes.md`

Auto-reconnect for up to **15s** when an active NDI connection drops unexpectedly. User-initiated `Stop` never auto-retries; explicit `Reconnect` restarts with the last `SourceId`. All timing is `TimeProvider`-driven and all observable mutations marshal to the UI thread — the ViewModel stays in Core (MAUI-free) and unit-testable.

### Bridge contract
| Member | Path | Notes |
|--------|------|-------|
| `ConnectionState { Connecting, Connected, Disconnected }` | `src/Core/NdiBridge/NdiBridgeModels.cs` | Plain C# enum — no NDI SDK types cross the bridge |
| `ConnectionState GetConnectionState()` | `src/Core/NdiBridge/INdiBridges.cs` (`INdiViewerBridge`) | Polled by the VM state machine to detect drops |
| Real impl | `src/MauiApp/NdiBridge/NdiViewerBridge.cs` | Superseded the stub in #277: 3 s frame-arrival watchdog + `recv_get_no_connections` inside the video pump; also raises `ConnectionStateChanged` on the pump thread |

### `IMainThreadDispatcher` abstraction (NEW)
| Item | Path | Notes |
|------|------|-------|
| Core interface | `src/Core/Services/IMainThreadDispatcher.cs` | `void Invoke(Action)` + `Task InvokeAsync(Func<Task>)` |
| MAUI impl | `src/MauiApp/Services/MauiMainThreadDispatcher.cs` | Wraps `MainThread.BeginInvokeOnMainThread` / `InvokeOnMainThreadAsync` |
| Why | — | Core cannot reference MAUI, so timer/poll callbacks dispatch via this seam; unit tests use a synchronous inline fake |

### DI registrations added in `MauiProgram.cs`
```csharp
// Reconnection infrastructure: system clock + UI-thread dispatcher abstraction.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IMainThreadDispatcher, MauiMainThreadDispatcher>();
```

### `ViewerViewModel` (`src/Core/Features/Viewer/ViewModels/ViewerViewModel.cs`)
State machine: `Idle → Connecting → Connected → Dropped → Retrying(countdown) → {Reconnected | Failed}`.
- Ctor injects `INdiViewerBridge`, `TimeProvider`, `IMainThreadDispatcher`.
- Timers (all `TimeProvider.CreateTimer`): monitor poll (1s) detects drops; attempt loop (2s) does full `StopReceiver→StartReceiver`, stops on first `Connected`; countdown (1s) drives remaining-seconds text.
- Window: 15s total; terminal failure after expiry.

| Member | Type | Purpose |
|--------|------|---------|
| `IsReconnecting` | `[ObservableProperty] bool` | Drives retry label + Cancel button visibility |
| `RetryStatusMessage` | `[ObservableProperty] string?` | `"Reconnecting... {n}s remaining"` |
| `CanReconnect` | `[ObservableProperty] bool` | Drives Reconnect button (`NotifyCanExecuteChangedFor`) |
| `CancelRetryCommand` | `[RelayCommand]` | Aborts the window immediately → stopped (FR6) |
| `ReconnectCommand` | `[RelayCommand(CanExecute = CanReconnect)]` | Restarts with last `SourceId` from error state (FR7) |

Terminal message constant: `"Connection lost. Reconnection failed."` Drop while playing → `"Connection lost. Reconnecting..."`.

UI: `src/MauiApp/Features/Viewer/Views/ViewerPage.xaml` — retry-status label, Cancel button (visible while `IsReconnecting`), Reconnect button (visible while `CanReconnect`).

See `docs/architecture.md` for the canonical module/threading diagram (already updated by architect — do not duplicate here).

## Conventional Commits
```
feat(settings): add developer tools section

Task: T006
Issue: #142
Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

## Android Manifest Requirements
Declared in `src/MauiApp/Platforms/Android/AndroidManifest.xml`: `INTERNET`, `ACCESS_WIFI_STATE`, `ACCESS_NETWORK_STATE`, `CHANGE_WIFI_MULTICAST_STATE`, `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_MEDIA_PROJECTION`, `FOREGROUND_SERVICE_CAMERA`, `FOREGROUND_SERVICE_MICROPHONE`, `CAMERA`, `RECORD_AUDIO`, `POST_NOTIFICATIONS`.

**GOTCHA — the manifest is silently ignored without a csproj property.** This project does not use MAUI SingleProject, so `src/MauiApp/NdiForAndroid.csproj` **must** keep `<AndroidManifest>Platforms\Android\AndroidManifest.xml</AndroidManifest>`. Before this fix the APK shipped with only `INTERNET`, breaking multicast discovery and all capture. Never remove that property.

## NDI Native Libraries
Location: `src/MauiApp/Platforms/Android/libs/<abi>/libndi.so` (NDI SDK 6.3.1) — `arm64-v8a` + `armeabi-v7a` only. Packaged by the default `AndroidNativeLibrary` globs — do **NOT** add an explicit `<AndroidNativeLibrary Include>` (double-add, XA4301). No x86/x86_64 binary exists: `NdiRuntime.EnsureInitialized()` returns `false` there and NDI features soft-disable (app must not crash on emulators).

## Local AI Integration (Ollama — credit reduction)

Ollama is installed at `%LOCALAPPDATA%\Programs\Ollama\ollama.exe` and runs at `http://localhost:11434`.

Helper script: `.github/scripts/ollama-task.ps1`

```powershell
# Usage examples:
.\.github\scripts\ollama-task.ps1 -Task code      -Prompt "Generate ISettingsRepository stub"
.\.github\scripts\ollama-task.ps1 -Task classify  -Prompt "<paste build log here>"
.\.github\scripts\ollama-task.ps1 -Task message   -Prompt "T003 complete, write GitHub comment"
.\.github\scripts\ollama-task.ps1 -Task test-stub -Prompt "Stub for SettingsViewModel.ApplyAsync"
.\.github\scripts\ollama-task.ps1 -Task summarize -Prompt "<paste issue body>"
```

| Task type | Model | Use for |
|-----------|-------|---------|
| `code` | `qwen2.5-coder:7b` | XAML boilerplate, C# stubs, repository skeletons |
| `test-stub` | `qwen2.5-coder:7b` | xUnit test class stubs with Moq |
| `message` | `qwen2.5-coder:7b` | GitHub issue comments, PR descriptions |
| `classify` | `phi3:mini` | Build log pass/fail, CI output triage |
| `summarize` | `phi3:mini` | Issue body summaries, task descriptions |

**Reserve cloud AI for**: architecture decisions, cross-file refactoring, novel debugging, NDI/MAUI API questions, anything requiring deep reasoning across multiple files.

### Verify Ollama is running
```powershell
Invoke-RestMethod http://localhost:11434/api/tags   # lists available models
# or
Get-Process ollama -ErrorAction SilentlyContinue
```

## CI / Emulator Test Patterns

- **Node.js version**: `22` (upgraded from 20 — EOL June 2026). Do NOT revert to 20.
- **Emulator cold-start**: always poll `sys.boot_completed=1` before `adb install` — see `testing/e2e/scripts/run-emulator-tests.sh`
- **UI test timeouts**: `ClickNav` base wait = **30s**, `AssertPageVisible` = **30s** for cold emulator. Do not reduce below 30s.
- **APK to install**: always use `com.ndi.android-Signed.apk` (not the unsigned variant) — path: `src/MauiApp/bin/Debug/net10.0-android/com.ndi.android-Signed.apk`
- **`DELETE_FAILED_INTERNAL_ERROR`** on `adb uninstall` is harmless — package was not installed; `adb install` will succeed.
- **`INSTALL_PARSE_FAILED_NO_CERTIFICATES`** means unsigned APK was used — switch to `-Signed.apk`.

## MAUI Theming Rules (lessons from #142)

- **Always `DynamicResource`** — `StaticResource` resolves once at XAML parse time; runtime theme changes have no effect.
- **Writing to resources**: `Application.Current.Resources["Key"] = value` at the top-level dict overrides merged dicts and fires all `DynamicResource` listeners immediately.
- **Shell colors at runtime**: must use `shell.SetValue(Shell.TabBarBackgroundColorProperty, color)` — XAML-set Shell colors are static.
- **Android status bar**: `WindowCompat.SetDecorFitsSystemWindows(Window, false)` + `Window.AddFlags(DrawsSystemBarBackgrounds)` in `MainActivity.OnCreate()`. Call `UpdateAndroidStatusBar()` from `MauiAppearanceService` on every theme change.
- **`Color.ToAndroid()` unavailable**: use `new Android.Graphics.Color((byte)(r*255), (byte)(g*255), (byte)(b*255), (byte)(a*255))` instead.
- **RadioButton**: native Android `MaterialRadioButton` ignores `DynamicResource` — use pure MAUI `ControlTemplate` with two `Ellipse` elements.

## Agent Workflow Lessons

- **Branch first, plan second**: always create and check out the feature branch (Stage -1) before running `feature.planner`. Planner commits the plan file to whatever branch is checked out.
- **Pass branch explicitly**: when delegating to sub-agents, always state `Current branch: feature/XXX-slug` in the prompt so they don't commit to `main` or a stale branch.
- **Stop agents on wrong branch**: if an agent is found to be working on the wrong branch, stop it immediately. Re-run it with the correct branch after checkout.
- **Verify branch before push**: sub-agents should run `git branch --show-current` before any commit.


- **Read this file first** — do not re-read `docs/constitution.md` or `docs/architecture.md` unless you need the full detail
- **Use Ollama for small tasks** — run `.github/scripts/ollama-task.ps1` for boilerplate, log classification, and issue messages before spending cloud AI credits
- **Stage 5 shortcut**: if task breakdown (T001–T010) already exists in GitHub, call `implementer` directly — do NOT go through `orchestrator`
- **One build per task** — run `dotnet build` after each task, not after each file edit
- **Batch GitHub reads** — fetch issue + child issues in one call, not one-by-one
- **Reuse branch** — always check `git branch --show-current` before creating a new branch
