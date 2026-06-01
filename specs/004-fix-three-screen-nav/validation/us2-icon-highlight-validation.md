# US2 Validation: Icon Mapping and Active Highlight Consistency

Date: 2026-03-17
Story: US2

## Scope

- Home/Stream/View icon mapping is house/camera/screen.
- Exactly one top-level destination is highlighted at a time.
- Stream remains active for stream setup/control routes.
- View remains active for view root/viewer routes.

## Implemented Changes

- Added vector drawables:
  - `app/src/main/res/drawable/ic_nav_home.xml`
  - `app/src/main/res/drawable/ic_nav_stream.xml`
  - `app/src/main/res/drawable/ic_nav_view.xml`
- Applied icon mapping in `app/src/main/res/menu/top_level_navigation_menu.xml`.
- Extended `TopLevelNavViewModel` with canonical `destinationItems` state and icon mapping.
- Added `onNavDestinationObserved()` to keep selected destination synchronized with actual nav destination.
- Added destination-change listener in `MainActivity` to map:
  - Home route -> HOME
  - Stream + output control routes -> STREAM
  - View + viewer routes -> VIEW
- Added stream top-level destination hint to `OutputControlUiState` and related test coverage.

## Test Coverage Added

- `TopLevelNavViewModelTest`
  - icon mapping contract assertions
  - single-selected destination assertions
- `OutputControlViewModelTopLevelNavTest`
  - stream setup/control maps to STREAM
- `TopLevelDestinationHighlightUiTest`
  - instrumentation scaffold for tap + deep-link matrix

## Validation Status

- `:app:testDebugUnitTest` -> PASS
- `:feature:ndi-browser:presentation:testDebugUnitTest` -> BLOCKED by unrelated compile errors in `HomeViewModelTest.kt` (existing workspace issue)
