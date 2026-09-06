# Feature: Home Quick Actions — Resume Output / Start Viewing Last Source (Issue #328)

## Overview

The Home dashboard shows two quick-action buttons, "Start Viewing Last Source" and "Resume
Output", whose commands currently navigate to hard-coded, non-existent routes
(`view-tab?sourceId=`, `stream-tab?streamName=`) that nothing on the receiving page consumes.
This feature makes both actions navigate correctly using the app's real navigation contracts, and
adds visibility/enabled rules so a quick action is never shown as tappable when it has nothing to
act on.

## User Stories

- As a user who previously viewed an NDI source, I want to tap "Start Viewing Last Source" on Home
  and land directly in the viewer for that source, so I don't have to find it again in the source
  list.
- As a user who previously had an NDI output running, I want to tap "Resume Output" on Home and
  land on the Stream tab with my settings already filled in, so I can resume broadcasting with one
  extra tap — without the app silently re-requesting screen-capture (MediaProjection) permission on
  my behalf.
- As a user who has never viewed a source or run output, I want the corresponding quick action to
  be visibly disabled, so I don't tap a button that does nothing.

## Functional Requirements

1. "Start Viewing Last Source" navigates to `viewer?sourceId={id}` (the same route the Sources
   list and deep-link handler already use) with the persisted last-viewed source id, URL-escaped.
2. "Start Viewing Last Source" is disabled when there is no persisted last-viewed source id.
3. "Resume Output" navigates to the Stream tab via `INavigationService.NavigateToPrimaryAsync`
   (never a hard-coded `//stream-tab`/`//stream-rail` route string) with a `resume=true` query
   parameter.
4. "Resume Output" never starts an output session and never triggers a MediaProjection consent
   prompt on its own — it only pre-populates the Stream tab so the user can review and tap Start.
5. "Resume Output" is disabled when there is no persisted output session to resume (no
   `IsOutputActive` snapshot, or no persisted stream name).
6. When the Stream tab is opened with `resume=true`, it pre-populates `StreamName` from the
   persisted app state and shows a status message inviting the user to tap Start. It does not mark
   the output as active and does not call `StartOutputAsync`.
7. Both quick actions re-evaluate their enabled state every time Home's existing refresh runs
   (page appearing, pull-to-refresh), so returning to Home after starting/stopping output or
   viewing a source updates the buttons without requiring an app restart.

## Non-Functional Requirements

- No new permission prompts are introduced by either quick action.
- No direct SQLite/database access from `HomeViewModel` — state continues to flow through
  `IAppStateRepository`.
- Enabled/disabled visuals use `DynamicResource`-bound values only (no hardcoded colors), per the
  project's MAUI theming rule.

## Success Criteria

- `HomeViewModelTests` verifies `StartViewingLastSourceCommand` calls
  `INavigationService.NavigateToAsync("viewer?sourceId=<escaped-id>")` exactly once when a last
  viewer source id is persisted, and is disabled (cannot execute) when it is not.
- `HomeViewModelTests` verifies `ResumeOutputCommand` calls
  `INavigationService.NavigateToPrimaryAsync(PrimaryNavDestination.Stream, "resume=true")` exactly
  once when a persisted active output session with a stream name exists, and is disabled when it
  does not.
- A new `OutputViewModelTests` case verifies that arriving with a resume request pre-populates
  `StreamName` from `IAppStateRepository` and sets a "tap Start" status message, without setting
  `IsOutputActive` and without calling `INdiOutputBridge.StartOutputAsync`.
- `dotnet build NdiForAndroid.sln` and `dotnet test tests/MauiApp.Tests` both pass.

## Out of Scope

- Implementing `INdiOutputBridge.IsActive` / corroborated output-session state (#326) — this
  feature only documents the interim gating rule to use until #326 lands, and the follow-up note
  for after it lands.
- Changing how "no longer discoverable" sources are detected for the viewer quick action beyond
  "was a last-viewed source id ever persisted" (see Assumptions).
- Any change to `SourceListViewModel`'s or `DeepLinkService`'s existing (already-correct)
  `viewer?sourceId=` / re-stream navigation.

## Assumptions

- "Start Viewing Last Source" gates only on whether `AppStateSnapshot.LastViewerSourceId` is
  non-empty — it does not check whether that source is currently discoverable on the network
  (`ViewerPage`/`ViewerViewModel` already handles an unreachable source with its own
  connect-failure UI). This matches the issue's recommendation (option c) and keeps the Home
  gating logic simple and synchronous.
- "Resume Output" gates on `AppStateSnapshot.IsOutputActive && !string.IsNullOrWhiteSpace(StreamName)`
  — the same condition the pre-existing (dead) runtime check already used — pending #326.

## Open Questions

None outstanding — the issue's open questions are resolved by the 2026-09-04 decision comment
(route choices) and by this plan's interim gating rule (disable, don't hide, using the persisted
snapshot only, pending #326).
