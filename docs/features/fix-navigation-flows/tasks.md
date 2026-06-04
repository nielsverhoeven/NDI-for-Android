# Tasks: Fix Broken Navigation Flows in MAUI App

## Summary
- Parent issue: #141
- Total tasks: 5
- Layers covered: Investigation, Shell/Routing, ViewModel, Service, Test, Platform/QA
- Branch: bugfix/141-fix-broken-navigation-flows

## Dependency Graph

```
T001 ──► T002 ──► T003 ──► T004
                        └──► T005
```

- T001 and T002 are sequential (audit builds on investigation findings)
- T003 is blocked by T002 (requires root-cause list)
- T004 and T005 can run in parallel once T003 is complete

## Task List

### T001: Investigate legacy Kotlin navigation vs MAUI Shell implementation
- **Layer**: Investigation
- **Description**: Compare the legacy Kotlin `NavController` deep-link routes (`ndi://sources`, `ndi://settings/discovery-servers`, `ndi://theme-editor`) found in `feature/ndi-browser/presentation/` against the current MAUI Shell route topology (`//sources`, `//settings`, registered routes `viewer`, `output` in `AppShell.xaml.cs`). Identify which routes existed in Kotlin but are missing, mis-named, or structurally different in MAUI Shell. Produce a written comparison note in `docs/features/fix-navigation-flows/investigation.md`.
- **Depends on**: none
- **Acceptance**: A written comparison note exists documenting which routes worked in Kotlin, their MAUI equivalents, and all identified gaps.
- **GitHub issue**: #163

---

### T002: Audit AppShell route registration and query-parameter plumbing
- **Layer**: Shell / Service
- **Description**: Inspect `src/MauiApp/AppShell.xaml`, `src/MauiApp/AppShell.xaml.cs`, `src/MauiApp/Services/ShellNavigationService.cs`, and `src/Core/Features/Sources/ViewModels/SourceListViewModel.cs`. Verify: (a) every route string used in ViewModels (`"viewer"`, `"output"` with `sourceId` query param) is registered via `Routing.RegisterRoute`; (b) `[QueryProperty]` attributes are present on `ViewerViewModel` and `OutputViewModel` to receive `sourceId`; (c) `ShellNavigationService.NavigateToAsync` surfaces exceptions rather than swallowing them silently. Document every gap as a numbered root cause.
- **Depends on**: T001
- **Acceptance**: A documented root-cause list exists identifying every broken route and missing query-property binding, confirmed against the investigation note from T001.
- **GitHub issue**: #164

---

### T003: Fix route registration, query-property bindings, and navigation error handling
- **Layer**: Shell / ViewModel / Service
- **Description**: Apply all fixes identified in T002: update `AppShell.xaml.cs` to register any missing or corrected routes; add `[QueryProperty]` attributes to destination ViewModels (`ViewerViewModel`, `OutputViewModel`) for the `sourceId` parameter; update `ShellNavigationService.NavigateToAsync` to log or re-throw on navigation failure; adjust `SourceListViewModel` navigation call strings if route names changed. `dotnet build` must pass cleanly after changes.
- **Depends on**: T002
- **Acceptance**: `dotnet build` exits 0; all four primary navigation paths (Sources tab → Viewer, Sources tab → Output, Settings tab, back from any) are structurally wired with no silent failure path in `ShellNavigationService`.
- **GitHub issue**: #165

---

### T004: Validate navigation flows on Android emulator
- **Layer**: Platform / QA
- **Description**: Deploy the Debug APK built from T003 to an Android emulator and manually walk all primary navigation paths: (1) Sources tab → select source → Viewer, (2) Sources tab → select source → Output, (3) Settings tab, (4) back-navigation from Viewer and Output back to Sources. Capture pass/fail evidence (logcat snippets and/or screenshots) in `test-results/141-navigation-validation-evidence.md`.
- **Depends on**: T003
- **Acceptance**: All four primary navigation paths complete without crash or dead-end; evidence file is committed to the branch.
- **GitHub issue**: #166

---

### T005: Add unit-test and UI-smoke coverage for navigation commands
- **Layer**: Test
- **Description**: In `tests/MauiApp.Tests/Features/Sources/SourceListViewModelTests.cs`, add tests verifying that `OpenViewer` and `OpenOutput` commands call `INavigationService.NavigateToAsync` with the correct route strings (including the `sourceId` query parameter). In `tests/MauiApp.UITests/`, add or update one Appium test confirming the Sources → Viewer navigation path reaches `ViewerPage` without error. All existing tests must continue to pass.
- **Depends on**: T003
- **Acceptance**: `dotnet test` exits 0 with ≥ 2 new navigation assertions verified; no regression in previously passing tests.
- **GitHub issue**: #167
