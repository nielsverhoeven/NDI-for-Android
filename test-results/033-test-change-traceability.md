# 033 FR-011 Test Change Traceability

Requirement reference: FR-011

## Modified Pre-existing Automated Tests

1. `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/source_list/SourceListUiStateTest.kt`
   - Triggering requirement(s): FR-013, FR-014
   - Why changed: Added consistency guard against mixed legacy/canonical button styles in Source List flow.

2. `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/viewer/ViewerViewModelTopLevelNavTest.kt`
   - Triggering requirement(s): FR-013, FR-014
   - Why changed: Added viewer style consistency contract for canonical button shape usage.

3. `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/output/OutputControlViewModelTopLevelNavTest.kt`
   - Triggering requirement(s): FR-013, FR-014
   - Why changed: Added output style consistency contract to prevent mixed-style regressions.

4. `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsMainNavigationStateTest.kt`
   - Triggering requirement(s): FR-002, FR-013, FR-014
   - Why changed: Added settings/discovery consistency checks for canonical styles across compact/wide discovery surfaces.

5. `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsLayoutTransitionTest.kt`
   - Triggering requirement(s): FR-005
   - Why changed: Added adaptive compact/wide spacing contract checks after button radius update.

6. `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsFragmentWideLayoutTest.kt`
   - Triggering requirement(s): FR-005
   - Why changed: Added readability/focus spacing invariants for wide layout.

7. `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/settings/SettingsScreenTest.kt`
   - Triggering requirement(s): FR-005
   - Why changed: Added compact readability/actionability assertions.

8. `testing/e2e/tests/033-fluent-button-radius.spec.ts`
   - Triggering requirement(s): FR-006, FR-007, FR-013, FR-014
   - Why changed: Added US2 mixed-style detection and US3 compact/wide + dark/light usability contract coverage.
