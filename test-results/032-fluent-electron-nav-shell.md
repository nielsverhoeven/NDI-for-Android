# 032 Fluent + Electron Nav Shell Evidence

Date: 2026-04-27

## Scope

- Top-level shell style-state baseline in navigation coordinator/host
- Home card token mapping baseline
- Source List, Viewer, Output shell-level visual consistency updates

## Evidence

- Shell style-state model + resolver:
  - `app/src/main/java/com/ndi/app/navigation/TopLevelNavigationCoordinator.kt`
  - `app/src/main/java/com/ndi/app/navigation/TopLevelNavigationHost.kt`
  - `app/src/main/java/com/ndi/app/navigation/TopLevelNavViewModel.kt`
- Shell invariant tests:
  - `app/src/test/java/com/ndi/app/navigation/TopLevelNavViewModelTest.kt`
  - `app/src/test/java/com/ndi/app/navigation/TopLevelNavigationCoordinatorTest.kt`
- Shared token helper baseline:
  - `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/home/FluentElectronHomeTokens.kt`

## Mixed Legacy/Redesigned Check

No mixed legacy and redesigned shell components were introduced in the shipped US1 flow files modified for feature 032.

## Classification

Pass
