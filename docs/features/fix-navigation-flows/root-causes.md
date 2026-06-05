# Root Causes: Navigation Flow Breaks

Parent issue: #141

## Audit scope

Audited files:

1. `src/MauiApp/AppShell.xaml`
2. `src/MauiApp/AppShell.xaml.cs`
3. `src/MauiApp/Services/ShellNavigationService.cs`
4. `src/Core/Features/Sources/ViewModels/SourceListViewModel.cs`
5. `src/MauiApp/Features/Viewer/Views/ViewerPage.xaml.cs`
6. `src/MauiApp/Features/Output/Views/OutputPage.xaml.cs`

## Numbered root-cause list

1. **Navigation failures were not observable at call sites**
   - Prior behavior allowed route failures to be effectively silent for callers.
   - Fix applied: `ShellNavigationService` now logs and rethrows in both `NavigateToAsync` and `GoBackAsync`.

2. **Legacy route expectations diverged from MAUI route topology**
   - Legacy deep links (`ndi://...`) do not map directly to MAUI Shell route names.
   - This caused ambiguity about expected route targets during migration and triage.

3. **Discovery-settings and theme-editor legacy paths are not structurally represented in MAUI shell**
   - `ndi://settings/discovery-servers` is currently represented by top-level `//settings` only.
   - `ndi://theme-editor` has no MAUI shell route/page mapping.

4. **Query-parameter binding location differs from initial assumptions**
   - `sourceId` query binding is implemented on destination Pages via `[QueryProperty]`, not on Core ViewModels.
   - This is valid in MAUI, but the split across projects (Core vs MAUI) makes it easy to misdiagnose route issues if reviewers expect ViewModel attributes.

## Verification snapshot

1. `viewer` and `output` are registered in `AppShell.xaml.cs`.
2. `SourceListViewModel` navigates using `viewer?sourceId=...` and `output?sourceId=...` with URI escaping.
3. `ViewerPage` and `OutputPage` each expose `[QueryProperty(nameof(SourceId), "sourceId")]` and forward values into their ViewModels.
4. `ShellNavigationService` now surfaces navigation exceptions via log + rethrow.
