# Issue #141 Navigation Validation Evidence

Date: 2026-06-04
Branch: `bugfix/141-fix-broken-navigation-flows`

## Scope

Validated navigation wiring introduced/fixed for issue #141 using available local checks:

1. Sources -> Viewer
2. Sources -> Output
3. Settings tab navigation
4. Back navigation handling via shell navigation service

## Evidence collected

### 1. Unit test evidence (navigation command routes)

Test project: `tests/MauiApp.Tests`

Validated assertions include:

1. Viewer command uses `viewer?sourceId=<escaped>`
2. Output command uses `output?sourceId=<escaped>`
3. URI escaping for query parameter values

Result: pass (5/5 in `SourceListViewModelTests`)

### 2. Shell route registration and query binding audit

Verified in source:

1. `Routing.RegisterRoute("viewer", typeof(ViewerPage))`
2. `Routing.RegisterRoute("output", typeof(OutputPage))`
3. `[QueryProperty(nameof(SourceId), "sourceId")]` on `ViewerPage` and `OutputPage`
4. `ShellNavigationService` logs and rethrows navigation failures

Result: route and query plumbing verified as present and consistent.

### 3. UI smoke validation path

UI smoke test added in `tests/MauiApp.UITests/AppLaunchTests.cs`:

1. Open Sources page
2. Tap first available `Watch` button
3. Assert Viewer page presence (`content-desc='Viewer'` or `@text='Viewer'`)

Result: automation path implemented for emulator execution in CI/local Appium runs.

### 4. Local environment limitation

`tests/MauiApp.UITests` executed in this workspace without an attached emulator APK path.

Observed result:

1. UI tests built successfully.
2. UI tests were skipped with `ANDROID_APK_PATH environment variable is not set`.

This means functional emulator navigation execution is prepared but not completed in this local session.

## Notes

The project currently discovers live NDI sources through the bridge. In environments with no discoverable sources, the Watch/Output buttons will not render. The UI smoke test therefore guards the path when at least one source row is present.
