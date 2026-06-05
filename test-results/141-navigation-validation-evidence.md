# Issue #141 Navigation Validation Evidence

Date: 2026-06-04
Branch: `bugfix/141-fix-broken-navigation-flows`

## Scope

Validated adaptive navigation parity for issue #141 using local checks and captured artifacts:

1. Four primary destinations remain wired (Home/Stream/View/Settings)
2. Adaptive placement behavior (left rail in landscape)
3. Navigation visual style parity improvements between portrait and landscape (T011)
4. Unit and UI test coverage updates for model/policy/viewmodel and orientation-aware navigation

## Evidence collected

### 1. Unit test evidence (T007)

Test project: `tests/MauiApp.Tests`

Added and validated navigation parity coverage in `tests/MauiApp.Tests/Features/Navigation/AdaptiveNavigationTests.cs`:

1. Metadata contains exactly four authoritative primary items and icon keys
2. Orientation policy resolves portrait -> bottom and landscape -> left rail
3. Adaptive shell state viewmodel exposes destination mapping and placement transitions

Result: pass (`dotnet test tests/MauiApp.Tests/MauiApp.Tests.csproj`) with 24/24 tests succeeded.

### 2. UI/integration test evidence (T008)

Test project: `tests/MauiApp.UITests`

Extended `tests/MauiApp.UITests/AppLaunchTests.cs` with adaptive navigation checks:

1. Portrait placement assertion path
2. Landscape placement assertion path
3. Four-destination click-through (Home/Stream/View/Settings)

Result in this workspace:

1. UI tests compile and execute successfully.
2. Runtime UI scenarios are skipped locally without `ANDROID_APK_PATH` / Appium environment.

### 3. T011 style parity implementation evidence

Code changes:

1. `src/MauiApp/AppShell.xaml`
: aligned tab bar palette to match rail palette (`#1C1C1E` background, white selected text, gray unselected text)
2. `src/MauiApp/AppShell.xaml.cs`
: aligned rail selection styling to tab-style behavior (no unique card highlight, color/opacity based state)

Device screenshots (captured 2026-06-05):

1. Landscape artifact: `test-results/141-nav-landscape.png`
2. Portrait capture artifact: `test-results/141-nav-portrait.png`

Reference portraits from issue discussion:

1. Portrait sample: `https://github.com/user-attachments/assets/2785ba99-9394-4531-ae88-52307263b773`
2. Landscape sample: `https://github.com/user-attachments/assets/e9c4f1a2-74ff-4b71-b291-720fa46a819d`

Environment note:

- Connected device in this session consistently rendered a wide frame even when forcing portrait lock through ADB window rotation commands.
- Because of that device behavior, local portrait image capture did not present bottom-placement evidence in this environment.
- Portrait bottom-placement behavior remains covered by the new UI test path and is expected to be validated in emulator/Appium pipeline runs.

### 4. Icon traceability table (T009)

| Legacy destination | MAUI destination enum | Route family | MAUI icon asset |
|---|---|---|---|
| Home | `PrimaryNavDestination.Home` | `//home-*` | `nav_home.svg` |
| Stream | `PrimaryNavDestination.Stream` | `//stream-*` | `nav_stream.svg` |
| View | `PrimaryNavDestination.View` | `//view-*` | `nav_view.svg` |
| Settings | `PrimaryNavDestination.Settings` | `//settings-*` | `nav_settings.svg` |

## Notes

1. The project discovers live NDI sources through the bridge; when no sources are found, Watch/Output action paths are naturally absent.
2. T007 and T008 implementation is in branch commits and test projects, but child issue close/sync must still be performed in GitHub issue workflow (T010).
3. T011 style parity changes are implemented and evidenced by code + landscape artifact; portrait runtime capture in this session is constrained by device rotation behavior.
