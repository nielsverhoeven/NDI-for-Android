# NDI-for-Android — Agent Knowledge Base
<!-- Last updated: 2026-06-15 | Read this INSTEAD of re-reading constitution.md + architecture.md for implementation tasks -->

## Tech Stack (authoritative)
- **Platform**: .NET MAUI `net10.0-android` | **Language**: C# 12, nullable enabled
- **UI**: MAUI Shell + XAML | **MVVM**: CommunityToolkit.Mvvm (`ObservableObject`, `[RelayCommand]`, `[ObservableProperty]`)
- **DI**: `Microsoft.Extensions.DependencyInjection` via `MauiProgram.cs` — no service locator
- **Persistence**: SQLite-net-pcl (async API only) | **NDI**: P/Invoke against `libndi.so`
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
| NDI bridge interfaces | `src/Core/NdiBridge/` |
| NDI bridge impl | `src/MauiApp/NdiBridge/` |
| SQLite/Data layer | `src/MauiApp/Data/` |
| Android platform services | `src/MauiApp/Platforms/Android/` |
| Unit tests | `tests/MauiApp.Tests/` |
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
4. **NDI threading**: marshal callbacks to UI thread via `MainThread.BeginInvokeOnMainThread`
5. **Android APIs** isolated in `Platforms/Android/` behind interfaces

## Shell Routes

Portrait (TabBar):

| Route | Page | Purpose |
|---|---|---|
| `//home-tab` | `SourceListPage` | Home tab — NDI discovery + tap-to-view |
| `//stream-tab` | `OutputPage` | Stream tab — outgoing NDI output only; no `sourceId` param |
| `//view-tab` | `SourceListPage` | View tab — same page as Home; discovery + tap-to-view |
| `//settings` | `SettingsPage` | Settings |
| `viewer?sourceId={id}` | `ViewerPage` | Pushed relative to current tab; registered via `Routing.RegisterRoute("viewer", typeof(ViewerPage))` in `AppShell.xaml.cs` |

Landscape (left navigation rail):

| Route | Page |
|---|---|
| `//home-rail` | `SourceListPage` |
| `//stream-rail` | `OutputPage` |
| `//view-rail` | `SourceListPage` |
| `//settings-rail` | `SettingsPage` |

`AppShell` selects portrait vs. landscape routes from `AdaptiveShellStateViewModel.IsLeftRailNavigationVisible`.

## Settings Feature (Issue #142 — MERGED to main, PR #211)
- **ViewModel**: `SettingsViewModel` — 5 sections: General, Appearance, Discovery, DeveloperTools, About
- **Model**: `NdiSettingsSnapshot` — holds all persisted settings as immutable record
- **Sections**: `SettingsSection` enum drives `IsXxxSectionSelected` properties
- **Apply flow**: user stages changes → clicks Apply → `ApplyAsync()` validates → saves via `ISettingsRepository`
- **Pending state**: `HasPendingChanges` tracked by comparing current state to `_baselineSnapshot`
- **Platform info**: `ISettingsPlatformService.GetAppInfo()` → `SettingsAppInfo(AppName, Version, Build)`
- **Cached sources**: loaded from `ISourceRepository.GetCachedSourcesAsync()`
- **Appearance service**: `IAppearanceService` / `MauiAppearanceService` — central runtime color application
  - `DarkPalette` / `LightPalette` records hold all 16 semantic color values
  - `UpdateResources()` writes to `Application.Current.Resources` (DynamicResource triggers)
  - `UpdateShell()` sets Shell tab bar, title, foreground via `SetValue()`
  - `UpdateAndroidStatusBar()` — `#if ANDROID` guard — sets status bar color via `WindowCompat`
- **Color system**: `Colors.xaml` defines 16 semantic keys (e.g. `PageBackground`, `ShellBackground`, `Primary`). ALL elements must use `DynamicResource` — never `StaticResource` or hardcoded hex.
- **RadioButton**: uses pure MAUI `ControlTemplate` (two `Ellipse` elements) — native Android `MaterialRadioButton` ignores `DynamicResource`.

## NDI Integration Rework (Issue #213 — **MERGED**, PR #229, branch: feature/213-ndi-integration-rework)

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

## Conventional Commits
```
feat(settings): add developer tools section

Task: T006
Issue: #142
Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

## Android Manifest Requirements
`INTERNET`, `CHANGE_WIFI_MULTICAST_STATE`, `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_MEDIA_PROJECTION` (API 34+)

## NDI Native Libraries
Location: `src/MauiApp/Platforms/Android/libs/` — included as `<AndroidNativeLibrary>` — `arm64-v8a` + `armeabi-v7a`

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
