# Spec 023 Continuity Regression

Date: 2026-03-31
Feature: 023-per-source-frame-retention

## Goal

Confirm that the existing single-source relaunch continuity path still works after introducing per-source source-list thumbnail retention.

## Baseline Reference

- Existing continuity evidence: test-results/021-server-correlation-checklist-20260329.md
- Existing continuity-focused tests:
  - feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/repository/ViewerContinuityRepositoryImplTest.kt
  - feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/viewer/ViewerViewModelRestoreTest.kt

## Validation Executed

Command:

```powershell
.\gradlew.bat :feature:ndi-browser:data:testDebugUnitTest --tests "*ViewerContinuityRepositoryImplTest*" :feature:ndi-browser:presentation:testDebugUnitTest --tests "*ViewerViewModelRestoreTest*"
```

Result:

- BUILD SUCCESSFUL in 16s
- `ViewerContinuityRepositoryImplTest` passed
- `ViewerViewModelRestoreTest` passed

## Interpretation

The existing single-source continuity path remains intact:

- `ViewerContinuityRepositoryImpl` still persists and restores `lastFrameImagePath` for relaunch continuity.
- `ViewerViewModelRestoreTest` confirms unavailable restore behavior still shows the saved preview without autoplay.
- Spec 023 adds `PerSourceFrameRepository` alongside the continuity repository rather than replacing it, so source-list thumbnail retention and relaunch continuity remain separate concerns.

## Conclusion

No continuity regression detected from spec 023.
