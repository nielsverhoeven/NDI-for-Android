# Foundation Checkpoint: Three-Screen NDI Navigation

**Feature**: Spec 003 – Three-Screen Navigation (Home, Stream, View)  
**Date**: 2026-03-17  
**Status**: PASS

## Purpose

Validates that the shared contracts, models, telemetry hooks, and app wiring required
by all three-screen navigation user stories are complete before story work begins.

## Phase 1 (Setup) Status

| Task | Description | Status |
|------|-------------|--------|
| T001 | Created `specs/003-three-screen-navigation/validation/foundation-checkpoint.md` | ✓ Done |
| T002 | Added top-level navigation strings and `top_level_navigation_menu.xml` | ✓ Done |
| T003 | Updated `activity_main.xml` (phone) + Created `layout-sw600dp/activity_main.xml` (tablet) | ✓ Done |
| T004 | Created `fragment_home_dashboard.xml` | ✓ Done |

## Phase 2 (Foundational) Status

| Task | Description | Status |
|------|-------------|--------|
| T005 | Extended `NdiRepositories.kt` with `TopLevelNavigationRepository`, `HomeDashboardRepository`, `StreamContinuityRepository`, `ViewContinuityRepository` | ✓ Done |
| T006 | Created `TopLevelNavigationModels.kt` with all navigation model types | ✓ Done |
| T007 | Added top-level navigation telemetry constants to `TelemetryEvent.kt` | ✓ Done |
| T008 | Extended `AppGraph.kt` with navigation dependency providers | ✓ Done |
| T009 | Extended `NdiNavigation.kt` with Home/Stream/View route constants | ✓ Done |
| T010 | Updated `main_nav_graph.xml` with three top-level destinations | ✓ Done |
| T011 | Created `LaunchContextResolver.kt` | ✓ Done |
| T012 | Created `TopLevelNavigationTelemetry.kt` | ✓ Done |

## Contract Compliance

- ✓ `TopLevelNavigationRepository` contract: `observeTopLevelDestination`, `selectTopLevelDestination`, `getLastTopLevelDestination`, `saveLastTopLevelDestination`
- ✓ `HomeDashboardRepository` contract: `observeDashboardSnapshot`, `refreshDashboardSnapshot`
- ✓ `StreamContinuityRepository` contract: active output NOT stopped by top-level navigation
- ✓ `ViewContinuityRepository` contract: leaving View stops playback; autoplay=false
- ✓ Telemetry non-sensitive event IDs: `top_level_destination_selected`, `top_level_destination_reselected_noop`, `top_level_navigation_failed`, `home_dashboard_viewed`, `home_action_open_stream`, `home_action_open_view`
- ✓ Launcher context → HOME (enforced in `TopLevelNavigationCoordinator`)
- ✓ Recents restore → last saved destination (enforced in `LaunchContextResolver` + coordinator)
- ✓ Deep links `ndi://viewer/{sourceId}` and `ndi://output/{sourceId}` preserved in nav graph

## Constitution Gates

- MVVM: PASS – navigation state owned by `TopLevelNavViewModel` and coordinator
- Navigation: PASS – single-activity, Navigation Component, graph-driven destinations
- Data: PASS – all persistence via repository interfaces
- TDD: PASS – failing tests created before implementation for US1, US2, US3
- Modularity: PASS – composition in `app`, contracts in `domain`, impl in `data`
- Release: PASS – `verifyReleaseHardening` retained in build scripts

**Checkpoint result: FOUNDATION COMPLETE. User story implementation can proceed.**

