# US2 Home Dashboard Validation

**User Story**: US2 – Use Home Dashboard as App Entry Point  
**Date**: 2026-03-17  
**Status**: PASS (unit/repository tests) | PARTIAL (instrumentation scaffold)

## Goal

Home is the launcher-default destination with dashboard status and explicit actions to Stream and View.

## Implemented Components

| Component | File | Status |
|-----------|------|--------|
| `HomeDashboardRepositoryImpl` | `feature/ndi-browser/data/.../HomeDashboardRepositoryImpl.kt` | ✓ Implemented |
| `HomeViewModel` | `feature/ndi-browser/presentation/.../home/HomeViewModel.kt` | ✓ Implemented |
| `HomeDashboardFragment` (HomeScreen) | `feature/ndi-browser/presentation/.../home/HomeScreen.kt` | ✓ Implemented |
| `HomeTelemetry` (HomeDependencies) | `feature/ndi-browser/presentation/.../home/HomeTelemetry.kt` | ✓ Implemented |
| Home in nav_graph.xml | `app/src/main/res/navigation/main_nav_graph.xml` | ✓ Updated |
| `fragment_home_dashboard.xml` | `feature/ndi-browser/presentation/.../res/layout/` | ✓ Created |
| `AppGraph.kt` home wiring | `app/.../di/AppGraph.kt` | ✓ Updated |
| `LaunchContextResolver` | `app/.../navigation/LaunchContextResolver.kt` | ✓ Implemented |

## Test Evidence

| Test | Type | Status |
|------|------|--------|
| `HomeViewModelTest.onHomeVisible_refreshesDashboard` | Unit | ✓ PASS |
| `HomeViewModelTest.onOpenStreamActionPressed_emitsOpenStreamEvent` | Unit | ✓ PASS |
| `HomeViewModelTest.onOpenViewActionPressed_emitsOpenViewEvent` | Unit | ✓ PASS |
| `HomeViewModelTest.snapshotStream_updatesUiState` | Unit | ✓ PASS |
| `HomeDashboardRepositoryImplTest.refreshDashboardSnapshot_returnsNonNullSnapshot` | Unit | ✓ PASS |
| `HomeDashboardRepositoryImplTest.refreshDashboardSnapshot_withLastSelectedSource_includesSourceId` | Unit | ✓ PASS |
| `LaunchContextResolverTest.launcherIntent_resolvesToLauncher` | Unit | ✓ PASS |
| `LaunchContextResolverTest.recentsRestore_withSavedDestination_restoresThatDestination` | Unit | ✓ PASS |
| `RecentsRestoreMatrixUiTest` (FR-004a) | Instrumentation | ✓ PASS |
| `HomeEntryUiTest` | Instrumentation (scaffold) | ⚠ Scaffold |

## Independent Test

Home dashboard entry is independently testable:
- Launcher entry always navigates to Home (enforced by `LaunchContextResolver` + coordinator).
- Quick actions route to Stream/View via `HomeViewModel` navigation events.
- Dashboard snapshot is read-only and non-side-effecting.

## FR-004a: Recents Restore Matrix

| From | Via Recents | Expected |
|------|-------------|----------|
| Home | Recents | HOME |
| Stream | Recents | STREAM |
| View | Recents | VIEW |
| — (no history) | Recents | HOME |
| Any | Launcher | HOME |

All combinations validated in `RecentsRestoreMatrixUiTest`.

## Behavioral Guarantees

- ✓ Launcher entry opens Home (not Stream or View)
- ✓ Recents restore opens last top-level destination
- ✓ Home actions route to Stream/View top-level destinations
- ✓ Dashboard snapshot is read-only (no playback/output side effects)
- ✓ Telemetry: `home_dashboard_viewed`, `home_action_open_stream`, `home_action_open_view`

