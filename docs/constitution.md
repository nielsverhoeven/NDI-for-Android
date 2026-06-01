# Project Constitution
<!-- Version: 1.0 — 2026-06-01 -->
<!-- Owned by: architect + orchestrator -->

This document is the authoritative source for technology choices, architecture principles, and development agreements. Every agent must read this file before starting any feature work. Amendments require `architect` review and a version increment.

---

## 1. Technology Stack

| Concern | Choice | Notes |
|---|---|---|
| Platform | .NET MAUI | Targeting `net9.0-android` |
| Language | C# 12 | Nullable reference types enabled |
| UI | MAUI Shell + XAML | URI-based navigation |
| MVVM | CommunityToolkit.Mvvm | `ObservableObject`, `[RelayCommand]`, `[ObservableProperty]` |
| Dependency Injection | `Microsoft.Extensions.DependencyInjection` via `MauiProgram.cs` | No service locator anti-pattern |
| Persistence | SQLite-net-pcl | Async API only |
| NDI Integration | P/Invoke against `libndi.so` | Existing arm64-v8a and armeabi-v7a binaries from `ndi/sdk-bridge/src/main/jniLibs/` |
| Build | `dotnet CLI` | `dotnet build`, `dotnet test` |
| Tests | xUnit 2.x + Moq | `dotnet test` |
| CI | GitHub Actions | `.github/workflows/` |

---

## 2. Architecture Principles

### 2.1 Layering

```
View (XAML)
  ↓ binds to
ViewModel (ObservableObject)
  ↓ calls
Repository Interface
  ↓ implemented by
Repository Implementation
  ↓ uses
NDI Bridge | SQLite | Platform Services
```

- **No direct database access from ViewModels** — always go through a repository interface.
- **No NDI types cross the bridge boundary** — bridge layer returns plain C# records only.
- **No business logic in Views** — Views are pure display; all logic in ViewModels or below.

### 2.2 Module Structure

```
src/
  MauiApp/
    MauiProgram.cs              ← DI registration root
    App.xaml / App.xaml.cs
    AppShell.xaml / AppShell.xaml.cs
    Platforms/
      Android/                  ← Android-specific implementations
        MainActivity.cs
        MainApplication.cs
    Features/
      Sources/                  ← NDI source discovery
        Models/
        Repositories/
        ViewModels/
        Views/
      Viewer/                   ← NDI video viewer
        Models/
        Repositories/
        ViewModels/
        Views/
      Output/                   ← NDI output / screen share
        Models/
        Repositories/
        ViewModels/
        Views/
      Settings/                 ← App settings
        Models/
        Repositories/
        ViewModels/
        Views/
    Services/                   ← Cross-cutting services (telemetry, logging)
    NdiBridge/                  ← P/Invoke wrapper (discovery, viewer, output)
    Data/                       ← SQLite context and migrations
tests/
  MauiApp.Tests/                ← xUnit project
    Features/
    Services/
    NdiBridge/
```

### 2.3 NDI Bridge Pattern

The NDI bridge isolates all native P/Invoke calls:

1. **Interface** defined in `NdiBridge/` (e.g., `INdiDiscoveryBridge`, `INdiViewerBridge`, `INdiOutputBridge`).
2. **Implementation** in `NdiBridge/` loads `libndi.so` via `[DllImport("ndi")]` and marshals results to C# records.
3. **Mock implementation** in `tests/` for unit testing without native library.
4. **Threading rule**: NDI callbacks arrive on native threads; all marshaling to UI thread uses `MainThread.BeginInvokeOnMainThread`.

NDI native binaries (`.so` files) must be included in the Android project as `AndroidNativeLibrary` build items.

### 2.4 Navigation

Use MAUI Shell with URI routing:

- `//sources` — Source list (home page)
- `//sources/viewer?sourceId={id}` — NDI viewer
- `//sources/output?sourceId={id}` — NDI output
- `//settings` — Settings

Register routes in `AppShell.xaml.cs` using `Routing.RegisterRoute`.

---

## 3. Testing Standards

- Every `Repository` class has a corresponding `*Tests.cs` in `tests/`.
- Every `ViewModel` has a corresponding `*Tests.cs`.
- Minimum per public method: **one happy-path test + one error-path test**.
- Tests must not depend on the NDI native library — use the mock bridge.
- NDI native validation runs only on-device or on physical emulators (T011).
- `dotnet test` must pass (non-NDI tests) before any PR merge.

---

## 4. Development Agreements

1. **`dotnet build` must pass after every task** — do not accumulate build failures.
2. **Conventional commits** — `feat(<layer>): description` with `Task: T###` and `Issue: #N` trailers.
3. **Branch per issue** — `feature/<issue>-<slug>` or `bugfix/<issue>-<slug>`.
4. **Constitution violations escalate to `orchestrator`** — never proceed silently.
5. **No secrets in source** — API keys, tokens, and device IDs must use `dotnet user-secrets` or environment variables.
6. **Nullable enabled** — `<Nullable>enable</Nullable>` in all project files; no `!` suppressions without a comment.
7. **MAUI API uncertainty** → consult `maui.expert`. NDI API uncertainty → consult `ndi.expert`.

---

## 5. Android-Specific Rules

- `AndroidManifest.xml` must declare: `INTERNET`, `CHANGE_WIFI_MULTICAST_STATE`, `FOREGROUND_SERVICE`.
- Screen-share output requires `FOREGROUND_SERVICE_MEDIA_PROJECTION` permission (API 34+).
- `libndi.so` binaries reside in `src/MauiApp/Platforms/Android/libs/` and are included as `<AndroidNativeLibrary>`.
- Minimum supported API: Android 8.0 (API 26).
- Target API: Android 15 (API 35).

---

## 6. CI Rules

- Workflow: `.github/workflows/android-ci.yml` (to be created/updated in T009).
- Timeout: `timeout-minutes: 30` on all jobs.
- Emulator-dependent tests run only when `ANDROID_EMULATOR_AVAILABLE` secret is set.
- `dotnet build` and non-NDI unit tests must pass on every PR.
