# NDI for Android — MAUI Developer Runbook

## Overview

This document describes how to build, run, test, and extend the .NET MAUI port of NDI for Android.

---

## Prerequisites

| Tool | Version | Install |
|---|---|---|
| .NET SDK | 10.0+ | https://dotnet.microsoft.com/download |
| MAUI Android workload | 10.0.20+ | `dotnet workload install maui-android` |
| Android SDK | API 26–35 | Android Studio or `sdkmanager` |
| JDK | 21 (Temurin) | https://adoptium.net |

Verify setup:

```sh
dotnet workload list       # should show maui-android
dotnet --version           # 10.x
```

---

## Solution Structure

```
NdiForAndroid.sln
├── src/
│   ├── Core/               # Platform-agnostic library (net10.0)
│   │   ├── Features/       # Models, repository interfaces, ViewModels
│   │   ├── NdiBridge/      # INdiDiscoveryBridge, INdiViewerBridge, INdiOutputBridge
│   │   └── Services/       # INavigationService, ITelemetryService
│   └── MauiApp/            # Android MAUI app (net10.0-android)
│       ├── Features/       # XAML Views + Repository implementations
│       ├── NdiBridge/      # P/Invoke stub implementations
│       ├── Data/           # SQLite-net-pcl (NdiDatabase.cs)
│       ├── Converters/     # XAML value converters
│       ├── Platforms/      # Android-specific: MainActivity, Manifest, libndi.so
│       └── Resources/      # Styles, colors, fonts
└── tests/
    └── MauiApp.Tests/      # xUnit tests (net10.0, no Android runtime needed)
```

---

## Build

```sh
# Restore all projects
dotnet restore NdiForAndroid.sln

# Build Core only (fast)
dotnet build src/Core/NdiForAndroid.Core.csproj

# Build MAUI Android app
dotnet build src/MauiApp/NdiForAndroid.csproj -c Release

# Run unit tests (no device required)
dotnet test tests/MauiApp.Tests/MauiApp.Tests.csproj
```

---

## Running on a Device / Emulator

```sh
# List connected devices
adb devices

# Deploy and run on connected device
dotnet build src/MauiApp/NdiForAndroid.csproj -t:Run -f net10.0-android
```

---

## Architecture

### Layering

```
View (XAML)
  ↓ binds to
ViewModel (ObservableObject — CommunityToolkit.Mvvm)
  ↓ calls
Repository Interface  (in Core)
  ↓ implemented by
Repository Impl (in MauiApp) → NDI Bridge | SQLite
```

### NDI Bridge (P/Invoke)

`libndi.so` is included in `src/MauiApp/Platforms/Android/libs/` for both `arm64-v8a` and `armeabi-v7a`. The bridge classes in `src/MauiApp/NdiBridge/` now provide concrete P/Invoke-backed initialization and managed fallback behaviors while preserving a plain C# boundary model.

Current state:
- Native initialization attempts use `NDIlib_initialize` / `NDIlib_version` through `[DllImport("ndi")]`.
- Discovery/output reachability checks are performed through managed TCP probes.
- Viewer/output lifecycle is wired end-to-end through bridge interfaces consumed by ViewModels.
- No native SDK types escape `NdiBridge` into ViewModels or Views.

### Navigation

Shell URI routing:
- `//sources` — Source list (home)
- `viewer?sourceId={id}` — NDI viewer (registered route)
- `output?sourceId={id}` — NDI output (registered route)
- `//settings` — Settings

---

## NDI Discovery

`NdiDiscoveryBridge` now supports configured endpoint checks and discovery check classification (`NONE`, `ENDPOINT_UNREACHABLE`, `TIMEOUT`, `UNKNOWN`) using bridge-safe boundary models.

For end-to-end validation with live NDI discovery:

1. Deploy the app to a physical device on the same LAN as an NDI source.
2. Ensure `CHANGE_WIFI_MULTICAST_STATE` permission is granted.
3. Configure discovery host/port in Settings and trigger refresh from Sources.
4. Validate discovery check diagnostics from bridge-reported status.

---

## SQLite

The database is created at `FileSystem.AppDataDirectory/ndi.db3` on first run. Tables:
- `sources` — cached NDI sources
- `settings` — discovery host/port, developer mode flag

Schema is created via `CreateTableAsync` (sqlite-net-pcl). No migration framework — breaking schema changes require a fresh install or manual migration.

---

## CI

GitHub Actions workflow: `.github/workflows/maui-ci.yml`

Runs on every push to `main`, `feature/**`, and `bugfix/**` branches, and on all PRs:

1. Restore dependencies
2. Build Core library
3. Build MAUI Android app
4. Run unit tests
5. Upload test results artifact

---

## Adding a New Feature

1. Define interfaces in `src/Core/Features/<Feature>/`
2. Implement ViewModels in `src/Core/Features/<Feature>/ViewModels/`
3. Implement Views in `src/MauiApp/Features/<Feature>/Views/`
4. Implement Repositories in `src/MauiApp/Features/<Feature>/Repositories/`
5. Register in `MauiProgram.cs`
6. Register route in `AppShell.xaml.cs` (if navigable)
7. Add unit tests in `tests/MauiApp.Tests/Features/<Feature>/`

---

## Task Progress

See `docs/features/maui-migration/tasks.md` for the T001–T010 issue mapping under issue #113.

| Task | Issue | Status |
|---|---|---|
| T001 | #190 | ✅ Core models and contracts aligned |
| T002 | #191 | ✅ Repository and service migration wiring |
| T003 | #192 | ✅ ViewModel command/state parity |
| T004 | #193 | ✅ Shell routes and feature pages |
| T005 | #194 | ✅ Android lifecycle and foreground service abstraction |
| T006 | #195 | ✅ P/Invoke bridge boundary implementation |
| T007 | #196 | ✅ ViewModel unit coverage expanded |
| T008 | #197 | ✅ UI/integration test suite aligned (environment-skippable) |
| T009 | #198 | ✅ Migration docs and evidence updated |
| T010 | #199 | ✅ Parent issue sync and closure checklist updates |

---

## Running Emulator UI Tests Locally

### Prerequisites

1. **Android SDK** with API 35 emulator image:
   ```bash
   sdkmanager "system-images;android-35;google_apis;x86_64"
   avdmanager create avd -n maui-test -k "system-images;android-35;google_apis;x86_64"
   ```

2. **Node.js** (v20+) with Appium and UIAutomator2 driver:
   ```bash
   npm install -g appium
   appium driver install uiautomator2
   ```

3. **Build the debug APK**:
   ```bash
   dotnet publish src/MauiApp/NdiForAndroid.csproj -f net10.0-android -c Debug -o publish-output
   ```

### Running the tests

1. Start the emulator: `emulator -avd maui-test -no-window &`
2. Wait for boot: `adb wait-for-device`
3. Start Appium: `appium &`
4. Run tests:
   ```bash
   APPIUM_SERVER_URL=http://127.0.0.1:4723/ \
   ANDROID_APK_PATH=$(find publish-output -name "*.apk" | head -1) \
   dotnet test tests/MauiApp.UITests/MauiApp.UITests.csproj -c Release
   ```

> **Note**: If `ANDROID_APK_PATH` is not set or the Appium server is not reachable,
> all tests in `MauiApp.UITests` are skipped automatically — this is the expected
> behaviour in CI environments without an emulator.

### CI behaviour

The `emulator-tests.yml` workflow is gated on the `ANDROID_EMULATOR_AVAILABLE`
repository secret. When this secret is set, the workflow:

1. Boots an Android 35 (x86_64) emulator using `reactivecircus/android-emulator-runner`
2. Builds and installs the debug APK via `adb install`
3. Starts an Appium server and runs `dotnet test tests/MauiApp.UITests/`
4. Uploads test results as a CI artifact

When the secret is absent (default for forks and local PRs), the job is skipped entirely.
