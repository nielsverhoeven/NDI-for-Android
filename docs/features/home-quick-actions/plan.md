# Technical Plan: Home Quick Actions (Issue #328)

## Architecture Fit

Per `docs/architecture.md` Navigation rules: primary destinations (Home/Stream/View/Settings) must
go through `INavigationService.NavigateToPrimaryAsync(PrimaryNavDestination, string? queryString)`
— never a hard-coded `//x-tab`/`//x-rail` string — while the pushed `viewer?sourceId=` route uses
the existing `NavigateToAsync(string route)` overload, exactly as
`SourceListViewModel.NavigateToViewerAsync` and `DeepLinkService.NavigateToViewerAsync` already do.
Both quick actions currently violate this: they hard-code `view-tab?sourceId=` (`view-tab` never
declared a `sourceId` query property) and `stream-tab?streamName=` (`OutputPage` never declared a
`streamName` query property). This plan replaces both with the two proven patterns above.

## .NET MAUI Implementation Approach

### 1. `src/Core/Features/Home/ViewModels/HomeViewModel.cs`

Add `using NdiForAndroid.Features.Navigation.Models;` (for `PrimaryNavDestination`).

Add four new observable fields (alongside the existing ones):

```csharp
[ObservableProperty]
private string? _lastViewerSourceId;

[ObservableProperty]
[NotifyCanExecuteChangedFor(nameof(StartViewingLastSourceCommand))]
private bool _hasLastViewerSource;

[ObservableProperty]
private string? _lastOutputStreamName;

[ObservableProperty]
[NotifyCanExecuteChangedFor(nameof(ResumeOutputCommand))]
private bool _canResumeOutput;
```

`[NotifyCanExecuteChangedFor]` is a CommunityToolkit.Mvvm attribute (not yet used elsewhere in this
repo, but shipped in the already-referenced `CommunityToolkit.Mvvm 8.*` package): it makes the
generated command call `NotifyCanExecuteChanged()` whenever the property setter runs, so the
button's bound `IsEnabled` updates live.

In `RefreshAsync`, inside the existing `_dispatcher.BeginInvokeOnMainThread(() => { ... })` block
(same block that already sets `ViewerStatus`/`OutputStatus`), add:

```csharp
LastViewerSourceId = state.LastViewerSourceId;
HasLastViewerSource = !string.IsNullOrWhiteSpace(state.LastViewerSourceId);

LastOutputStreamName = state.StreamName;
CanResumeOutput = state.IsOutputActive && !string.IsNullOrWhiteSpace(state.StreamName);
```

Replace the two command bodies:

```csharp
[RelayCommand(CanExecute = nameof(HasLastViewerSource))]
private async Task StartViewingLastSource()
{
    if (string.IsNullOrWhiteSpace(LastViewerSourceId))
        return;

    await _navigationService.NavigateToAsync($"viewer?sourceId={Uri.EscapeDataString(LastViewerSourceId)}");
}

[RelayCommand(CanExecute = nameof(CanResumeOutput))]
private async Task ResumeOutput()
{
    if (!CanResumeOutput)
        return;

    await _navigationService.NavigateToPrimaryAsync(PrimaryNavDestination.Stream, "resume=true");
}
```

Keep both method names unchanged so the generated command names (`StartViewingLastSourceCommand`,
`ResumeOutputCommand`) — already referenced by `HomePage.xaml` and the tests below — don't change.
Remove the `RestoreStateAsync()` call these bodies used to make: gating and navigation now read the
fields `RefreshAsync` already populated, so there's no redundant re-fetch. No DI/constructor change
needed — `HomeViewModel` already takes `INavigationService`.

### 2. `src/MauiApp/Features/Home/Views/HomePage.xaml`

Bind `IsEnabled` on both quick-action buttons (DynamicResource is only relevant to color/theme
setters already present — no color changes needed, MAUI's default `Button` visual already dims
when `IsEnabled="False"`, consistent with `OutputPage.xaml`'s existing `IsEnabled` bindings which
carry no extra opacity/converter treatment either):

```xml
<Button Text="Start Viewing Last Source"
        Command="{Binding StartViewingLastSourceCommand}"
        IsEnabled="{Binding HasLastViewerSource}"
        ... />

<Button Grid.Column="1"
        Text="Resume Output"
        Command="{Binding ResumeOutputCommand}"
        IsEnabled="{Binding CanResumeOutput}"
        ... />
```

Decision: **disabled, not hidden.** Hiding either button would leave an empty half of the
`Grid ColumnDefinitions="*,*"` quick-actions row with no replacement layout specified by the issue;
disabling keeps the two-column layout stable and discoverable. The binding is redundant with the
`[RelayCommand(CanExecute=...)]`-driven auto-disable MAUI already applies to `Button.Command`, but
makes the rule visible and testable straight from the XAML.

### 3. `src/MauiApp/Features/Output/Views/OutputPage.xaml.cs`

Add a third `[QueryProperty]` and branch to it in `OnAppearing`:

```csharp
[QueryProperty(nameof(ReStreamSourceId), "reStreamSourceId")]
[QueryProperty(nameof(IsReStreamMode), "isReStreamMode")]
[QueryProperty(nameof(ResumeRequested), "resume")]
public partial class OutputPage : ContentPage
{
    ...
    public string? ResumeRequested { get; set; }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _viewModel.LoadCommand.Execute(null);

        if (!string.IsNullOrEmpty(ReStreamSourceId))
            _viewModel.ApplyReStreamRequest(ReStreamSourceId, bool.TryParse(IsReStreamMode, out var b) && b);
        else if (bool.TryParse(ResumeRequested, out var resume) && resume)
            _viewModel.ApplyResumeRequestCommand.Execute(null);
    }
}
```

`resume` and `reStreamSourceId` are mutually exclusive entry points into the Stream tab (Home's
quick action vs. a Sources-page/deep-link handoff), so `else if` is correct.

### 4. `src/Core/Features/Output/ViewModels/OutputViewModel.cs`

Add a new command that mirrors the *safe* half of `OnAppResumed` — pre-populate, never
auto-activate:

```csharp
[RelayCommand]
private async Task ApplyResumeRequestAsync()
{
    var state = await _appStateRepo.RestoreStateAsync();
    if (state.IsOutputActive && !string.IsNullOrWhiteSpace(state.StreamName))
    {
        StreamName = state.StreamName;
        StatusMessage = "Tap Start to resume output.";
    }
}
```

Deliberately does **not** set `IsOutputActive = true` and does **not** call `StartOutputAsync` —
this is what keeps the "never silently re-request MediaProjection consent" decision true: the user
must always press Start themselves. This intentionally differs from `OnAppResumed`'s existing
optimistic `IsOutputActive = true` (that optimism is the defect tracked by #326/#327 — do not copy
it into new code). No DI/constructor change needed — `OutputViewModel` already takes
`IAppStateRepository`.

## Interim rule for the #326 dependency (read before implementing)

`INdiOutputBridge` has **no** `IsActive` (or equivalent corroborated running-state) signal today —
#326 (open, not yet implemented in this worktree) will add one. Until it lands, use this interim
rule:

- `CanResumeOutput` is computed **purely from the persisted `AppStateSnapshot`**
  (`IsOutputActive && StreamName` non-empty) — identical to the condition the pre-existing dead
  runtime check already used. Do not query `INdiOutputBridge` from `HomeViewModel`; that dependency
  doesn't exist yet and is out of scope for this issue.
- This mirrors #326's own documented safe fallback ("option 3": show "Tap Start to resume output"
  instead of optimistically claiming the session is restored) — `ApplyResumeRequestAsync` follows
  that same fallback for the Home-quick-action entry point.

**Follow-up once #326 lands** (do not implement now): `CanResumeOutput` must become `false` once
the bridge corroborates the sender is *already* running — per the issue decision, "Resume Output"
should only be offered "when the sender is not already running." Prefer extending whatever
`IAppStateRepository`/output-status read model #326 exposes `IsActive` through, over giving
`HomeViewModel` a direct `INdiOutputBridge` dependency, to keep Home's output picture
single-sourced.

## Testing Strategy

Unit tests only (no UI/NDI e2e needed — pure navigation/gating logic). See `tasks.md` for the exact
test method list.

- `HomeViewModelTests`: mock `IAppStateRepository.RestoreStateAsync()` per test to return the
  desired `LastViewerSourceId`/`IsOutputActive`/`StreamName` combination; assert on
  `Mock<INavigationService>` with `Verify(n => n.NavigateToAsync(expectedRoute), Times.Once)` and
  `Verify(n => n.NavigateToPrimaryAsync(PrimaryNavDestination.Stream, "resume=true"), Times.Once)`
  (same style as `SourceListViewModelTests.cs`); assert
  `sut.StartViewingLastSourceCommand.CanExecute(null)` / `sut.ResumeOutputCommand.CanExecute(null)`
  directly for the disabled cases.
- `OutputViewModelTests`: mock `IAppStateRepository.RestoreStateAsync()`, call
  `await sut.ApplyResumeRequestCommand.ExecuteAsync(null)`, assert `StreamName`, `StatusMessage`,
  that `IsOutputActive` stays `false`, and
  `_bridgeMock.Verify(b => b.StartOutputAsync(...), Times.Never)`.

## Risks

- Leaving the old `RestoreStateAsync()` call in a command body alongside the new field-based gating
  still works but adds a redundant round-trip — use the exact replacement bodies above.
- `Button.IsEnabled` is redundant with `CanExecute`; harmless, but keep both in sync if either
  changes later.

## Constitution Compliance

- Rule 1 (no direct DB access from ViewModels): both ViewModels continue through
  `IAppStateRepository`.
- Rule 2 (no NDI SDK types cross the bridge): no bridge changes at all in this feature.
- Rule 3 (no business logic in Views): XAML only gains binding expressions; `OutputPage.xaml.cs`'s
  new branch is lifecycle wiring, matching the existing `ReStreamSourceId` branch beside it.
- `DynamicResource` only — no new colors introduced.
