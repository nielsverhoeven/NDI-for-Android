# US1 Validation: View Routing and Deterministic Back Behavior

Date: 2026-03-17
Story: US1

## Scope

- View source selection opens Viewer route.
- Viewer back returns to View root.
- View root back returns to Home.

## Implemented Changes

- Added explicit View <-> Viewer action IDs and helpers.
- Added deterministic view-flow back policy in coordinator/viewmodel.
- Added activity back-dispatch handling for Viewer and View root destinations.
- Added unit regression coverage for route wiring and back policy.
- Added instrumentation scaffold tests for View/Viewer transition assertions.

## Validation Runs

- `:app:testDebugUnitTest` -> PASS
- `TopLevelNavigationCoordinatorTest` includes View->Viewer and back policy assertions.
- `TopLevelNavViewModelTest` includes Viewer->View and View->Home back assertions.

## Remaining Work

- Expand instrumentation scaffold assertions into full device-level checks during connected test execution.
