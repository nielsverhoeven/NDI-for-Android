# US3 Stream/View Continuity and Deep-Link Validation

**User Story**: US3 – Set Up and View NDI Streams Across Dedicated Pages  
**Date**: 2026-03-17  
**Status**: PASS (unit tests) | PARTIAL (instrumentation scaffold)

## Goal

Preserve existing Stream/View flows under three-screen IA, including deep links and
continuity semantics.

## Implemented Components

| Component | File | Status |
|-----------|------|--------|
| `StreamContinuityRepositoryImpl` | `feature/ndi-browser/data/.../StreamContinuityRepositoryImpl.kt` | ✓ Implemented |
| `ViewContinuityRepositoryImpl` | `feature/ndi-browser/data/.../ViewContinuityRepositoryImpl.kt` | ✓ Implemented |
| `ViewerViewModel` (leave-View stop) | Existing + top-level nav interop | ✓ Verified |
| `SourceListViewModel` (foreground refresh) | Existing + top-level nav interop | ✓ Verified |
| Deep link compatibility | `main_nav_graph.xml` + `NdiNavigation.kt` | ✓ Preserved |

## Test Evidence

| Test | Type | Status |
|------|------|--------|
| `ViewerViewModelTopLevelNavTest.leavingView_stopsPlayback` | Unit | ✓ PASS |
| `ViewerViewModelTopLevelNavTest.noAutoplay_onRelaunch` | Unit | ✓ PASS |
| `ViewerViewModelTopLevelNavTest.onBackToListPressed_doesNotPause_onlyStops` | Unit | ✓ PASS |
| `OutputControlViewModelTopLevelNavTest.activeOutput_remainsActiveAfterTopLevelNavigationAway` | Unit | ✓ PASS |
| `OutputControlViewModelTopLevelNavTest.processDeathRestore_doesNotAutoRestart` | Unit | ✓ PASS |
| `OutputControlViewModelTopLevelNavTest.idempotentStopGuard_preventsDoubleStop` | Unit | ✓ PASS |
| `DeepLinkTopLevelNavigationUiTest` | Instrumentation (scaffold) | ⚠ Scaffold |

## Deep Link Compatibility

- ✓ `ndi://viewer/{sourceId}` preserved in `main_nav_graph.xml`
- ✓ `ndi://output/{sourceId}` preserved in `main_nav_graph.xml`
- ✓ Deep link route constants in `NdiNavigation.kt`
- ✓ Both deep links must expose top-level chrome (nav bar/rail) — wired in `MainActivity`

## Stream Continuity Guarantees

| Scenario | Expected | Status |
|----------|----------|--------|
| Navigate Home/View while output is ACTIVE | Output remains ACTIVE | ✓ Verified |
| Process death + relaunch | Last-known status shown, explicit restart required | ✓ Verified |
| Auto-restart on restore | Prohibited (autoRestartPermitted=false) | ✓ Enforced |

## View Continuity Guarantees

| Scenario | Expected | Status |
|----------|----------|--------|
| Navigate away from View | Playback stops (STOPPED) | ✓ Verified |
| Return to View | Selected source highlighted, no autoplay | ✓ Verified |
| Process death + relaunch | Source ID restored as context, no autoplay | ✓ Enforced |

## Foreground Discovery Refresh

- ✓ `SourceListViewModel.onScreenVisible` → `startForegroundAutoRefresh` preserved
- ✓ `SourceListViewModel.onScreenHidden` → `stopForegroundAutoRefresh` preserved
- ✓ Discovery refresh remains foreground-bound regardless of top-level navigation shell

