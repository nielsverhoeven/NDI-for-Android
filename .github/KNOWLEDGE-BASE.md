# NDI-for-Android — Agent Knowledge Base
<!-- Last updated: 2026-06-07 | Read this INSTEAD of re-reading constitution.md + architecture.md for implementation tasks -->

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
- `//sources` — Source list (home)
- `//sources/viewer?sourceId={id}` — NDI viewer
- `//sources/output?sourceId={id}` — NDI output
- `//settings` — Settings

## Settings Feature (Issue #142 context)
- **ViewModel**: `SettingsViewModel` — 5 sections: General, Appearance, Discovery, DeveloperTools, About
- **Model**: `NdiSettingsSnapshot` — holds all persisted settings as immutable record
- **Sections**: `SettingsSection` enum drives `IsXxxSectionSelected` properties
- **Apply flow**: user stages changes → clicks Apply → `ApplyAsync()` validates → saves via `ISettingsRepository`
- **Pending state**: `HasPendingChanges` tracked by comparing current state to `_baselineSnapshot`
- **Platform info**: `ISettingsPlatformService.GetAppInfo()` → `SettingsAppInfo(AppName, Version, Build)`
- **Cached sources**: loaded from `ISourceRepository.GetCachedSourcesAsync()`

## Code Patterns

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

## Agent Efficiency Rules
- **Read this file first** — do not re-read `docs/constitution.md` or `docs/architecture.md` unless you need the full detail
- **Use Ollama for small tasks** — run `.github/scripts/ollama-task.ps1` for boilerplate, log classification, and issue messages before spending cloud AI credits
- **Stage 5 shortcut**: if task breakdown (T001–T010) already exists in GitHub, call `implementer` directly — do NOT go through `orchestrator`
- **One build per task** — run `dotnet build` after each task, not after each file edit
- **Batch GitHub reads** — fetch issue + child issues in one call, not one-by-one
- **Reuse branch** — always check `git branch --show-current` before creating a new branch
