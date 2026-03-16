# US2 Selection and Viewer Validation

Date: 2026-03-15

Independent test scope:
- Selection persistence and no-autoplay relaunch behavior.
- Viewer connect/play transitions.
- List-to-viewer navigation and tablet viewer rendering.

Evidence references:
- `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/selection/UserSelectionStateTest.kt`
- `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/viewer/ViewerViewModelTest.kt`
- `feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/UserSelectionRepositoryContractTest.kt`
- `feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/viewer/ViewerNavigationTest.kt`
- `feature/ndi-browser/presentation/src/androidTest/java/com/ndi/feature/ndibrowser/viewer/ViewerTabletUiTest.kt`

Verdict: US2 implementation artifacts present and ready for execution-time validation.
