# Project Constitution
<!-- Version: 2.0.1 — 2026-10-10 (PATCH clarification, #456: §5 target API follows the TargetFramework (net10.0-android → API 36 with the Android 36.x workload), min API 26 via SupportedOSPlatformVersion, no separate targetSdk property; §2.3/§5 native-lib packaging wording (default globs, not an explicit item; Release ships arm64-v8a + x86_64); §6 workflow names corrected to the shipped ndi-for-android-cicd.yml / emulator-tests.yml and the AAB + APK release output. Previous: 2.0 — 2026-09-27, amendment #395: §2.4 redefines the route contract — one TabBar route family; the two placement variants are chrome, not routes. 1.1 — 2026-07-07, NDI integration decision recorded as P/Invoke implemented, SDK 6.3.1; §2.4 navigation routes aligned with the shipped Shell routes) -->
<!-- Owned by: architect + orchestrator -->

This document is the authoritative source for technology choices, architecture principles, and development agreements. Every agent must read this file before starting any feature work. Amendments require `architect` review and a version increment.

---

## 1. Technology Stack

| Concern | Choice | Notes |
|---|---|---|
| Platform | .NET MAUI | Targeting `net10.0-android` |
| Language | C# 12 | Nullable reference types enabled |
| UI | MAUI Shell + XAML | URI-based navigation |
| MVVM | CommunityToolkit.Mvvm | `ObservableObject`, `[RelayCommand]`, `[ObservableProperty]` |
| Dependency Injection | `Microsoft.Extensions.DependencyInjection` via `MauiProgram.cs` | No service locator anti-pattern |
| Persistence | SQLite-net-pcl | Async API only |
| NDI Integration | P/Invoke against `libndi.so` — **decided and implemented** (no binding library) | Bundled NDI SDK ANDROID 6.3.1 binaries (arm64-v8a + armeabi-v7a) in `src/MauiApp/Platforms/Android/libs/`; Release packages ship arm64-v8a + x86_64 only (armeabi-v7a is not packaged); NDI soft-disables on ABIs without a binary (x86_64) |
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

NDI native binaries (`.so` files) are picked up from `Platforms/Android/libs/<abi>/` by the .NET for
Android default `AndroidNativeLibrary` globs. Do **not** add an explicit `<AndroidNativeLibrary Include>`
— it double-adds the library (XA4301). *(Clarified in v2.0.1.)*

### 2.4 Navigation

*Amended in v2.0 (#395): the two-variant route contract (`//xxx-tab` / `//xxx-rail`) is replaced by
one route family.*

Use MAUI Shell with URI routing. Every top-level destination has exactly **one** absolute route,
whichever navigation placement the adaptive policy selects:

- `//home` — Home dashboard
- `//stream` — NDI output (no `sourceId` parameter — output originates on this device)
- `//view` — Source list, tap-to-view
- `//settings` — Settings
- `viewer?sourceId={id}` — NDI viewer, pushed relative to the current destination

The bottom tab bar and the left rail are **chrome over the same `TabBar`, not route variants**: the
rail is the locked Shell flyout, and the bottom bar is hidden at ShellItem scope with
`Shell.TabBarIsVisible`. A placement change (rotation, window size class) must never navigate,
change `Shell.CurrentItem`, or reset a navigation stack. `PrimaryTabBar.IsVisible` must never be
written, and no second set of `ShellItem`s may be added for a placement.

Register pushed (non-tab) routes in `AppShell.xaml.cs` using `Routing.RegisterRoute`.
Full route table, the chrome rules and the size-class/orientation placement policy:
`docs/architecture.md` (Navigation).

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
- `libndi.so` binaries reside in `src/MauiApp/Platforms/Android/libs/<abi>/` and are packaged by the
  default `AndroidNativeLibrary` globs (no explicit item — see §2.3). Release packages ship
  `arm64-v8a` + `x86_64` (the .NET 10 default `RuntimeIdentifiers`); `armeabi-v7a` is not packaged.
- Minimum supported API: Android 8.0 (API 26), set by `SupportedOSPlatformVersion` in the csproj.
- Target API follows the `TargetFramework`: `net10.0-android` → API 36 with the Android 36.x workload.
  There is **no** separate targetSdk property — `AndroidMinSdkVersion`/`AndroidTargetSdkVersion` are
  not read by the SDK and must not be (re)added to the csproj. CI asserts `targetSdkVersion ≥ 36` on
  both the AAB and the APK. *(Amended in v2.0.1, #456.)*

---

## 6. CI Rules

- Workflows: `.github/workflows/ndi-for-android-cicd.yml` (unit tests → version → one Release
  publish producing the signed AAB **and** the universal APK → Appium e2e on the APK → GitHub
  release with both files, `main` only), `.github/workflows/emulator-tests.yml` (manual Appium
  emulator run), `.github/workflows/codeql.yml`. The AAB is the Google Play upload; the APK is the
  sideload/e2e package. *(Corrected in v2.0.1 — `release.yml` never existed.)*
- Release signing: passwords reach `dotnet publish` via the `file:` prefix from owner-only files in
  `$RUNNER_TEMP`; both signatures are pinned on the upload-key SHA-256 recorded in
  `.github/KNOWLEDGE-BASE.md` (Release signing). No secrets in source (§4.5).
- Timeout: `timeout-minutes: 30` on build jobs, `timeout-minutes: 40` on emulator jobs.
- Emulator tests always install a **Release** APK (not Debug) to avoid Mono Fast Deployment aborts.
- Before every `adb install` in CI scripts, run `adb uninstall com.ndi.android || true` to prevent `INSTALL_FAILED_UPDATE_INCOMPATIBLE` on cached emulators.
- `dotnet build` and non-NDI unit tests must pass on every PR.
- See `/android-ci-failure-patterns` skill for detailed diagnosis of Android emulator CI failures.

---

## Amendment history

| Version | Date | Issue | Type | Summary |
|---|---|---|---|---|
| 2.0.1 | 2026-10-10 | #456 | PATCH (clarification) | §5 target API follows the TFM (API 36), min API 26 via `SupportedOSPlatformVersion`, no targetSdk property; §2.3/§5 native-lib packaging via default globs, Release ABIs arm64-v8a + x86_64; §6 workflow names and AAB + APK release output. |
| 2.0 | 2026-09-27 | #395 | Amendment | §2.4 one TabBar route family; placement variants are chrome, not routes. |
| 1.1 | 2026-07-07 | — | Amendment | NDI integration recorded as P/Invoke implemented (SDK 6.3.1); §2.4 routes aligned with the shipped Shell routes. |
