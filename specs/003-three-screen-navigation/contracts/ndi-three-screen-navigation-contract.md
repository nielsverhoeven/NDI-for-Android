# Contract: Three-Screen Top-Level Navigation (Home, Stream, View)

## 1. Repository Contracts

### 1.1 TopLevelNavigationRepository

- observeTopLevelDestination(): stream of TopLevelDestinationState
- selectTopLevelDestination(destination: HOME | STREAM | VIEW, trigger):
  NavigationTransitionRecord
- getLastTopLevelDestination(): HOME | STREAM | VIEW?
- saveLastTopLevelDestination(destination): void

Behavioral requirements:

- Re-selecting an already active destination must be treated as a no-op without
  creating duplicate destination instances.
- Launcher launch context must resolve to HOME.
- Recents/task restore context may restore the last top-level destination.

### 1.2 HomeDashboardRepository

- observeDashboardSnapshot(): stream of HomeDashboardSnapshot
- refreshDashboardSnapshot(): HomeDashboardSnapshot

Behavioral requirements:

- Snapshot must only include non-sensitive metadata.
- Snapshot must summarize Stream and View status without initiating playback or
  output side effects.

### 1.3 StreamContinuityRepository

- observeContinuityState(): stream of StreamContinuityState
- captureLastKnownState(): void
- clearTransientStateOnExplicitStop(): void

Behavioral requirements:

- Leaving Stream through top-level navigation does not stop active output.
- Process-death restore may show last-known stream status but must not
  auto-restart output.

### 1.4 ViewContinuityRepository

- observeContinuityState(): stream of ViewContinuityState
- stopForTopLevelNavigation(): void
- getLastSelectedSourceId(): string?

Behavioral requirements:

- Leaving View through top-level navigation stops playback immediately.
- Returning to View preserves selected source context but does not autoplay.

## 2. ViewModel Contracts

### 2.1 TopLevelNavViewModel

Inputs:

- onAppLaunch(context: LAUNCHER | RECENTS_RESTORE | DEEP_LINK)
- onDestinationSelected(destination, trigger)
- onDeepLinkResolved(destination?)

Outputs (state):

- selectedDestination: HOME | STREAM | VIEW
- navLayoutProfile: PHONE_BOTTOM_NAV | TABLET_NAV_RAIL
- destinationItems: list with selected-state flags

Outputs (events):

- navigateToHome
- navigateToStream
- navigateToView
- navigationFailure(reasonCode)

Guarantees:

- Destination selection state is deterministic and single-source-of-truth.
- Repeated taps on selected destination emit no-op telemetry only.
- Navigation failures emit observable events without crashing UI.

### 2.2 HomeViewModel

Inputs:

- onHomeVisible()
- onOpenStreamActionPressed()
- onOpenViewActionPressed()

Outputs:

- dashboardSnapshot: HomeDashboardSnapshot
- navigationEvent: OpenStream | OpenView

Guarantees:

- Home actions route to top-level Stream or View destinations.
- Home rendering remains read-only with no direct repository side effects beyond
  snapshot refresh.

### 2.3 SourceListViewModel and ViewerViewModel Interop Constraints

Inputs/outputs remain as defined in existing feature contracts with these added
constraints:

- Top-level route changes must preserve foreground-only discovery refresh in
  source list behavior.
- Leaving View due to top-level route changes must stop active playback
  and keep selected source for no-autoplay restore.

### 2.4 OutputControlViewModel Interop Constraints

Inputs/outputs remain as defined in existing output contract with these added
constraints:

- Leaving Stream due to top-level route changes must not implicitly stop active
  output sessions.
- Process-death restore state is contextual only until explicit restart by user.

## 3. Navigation Contract

Top-level destinations:

- HomeRoute
- StreamRoute
- ViewRoute

Supporting routes retained:

- ViewerRoute(sourceId: string)
- OutputControlRoute(sourceId: string)

Rules:

- Home, Stream, and View must be reachable from each top-level destination.
- Top-level navigation must indicate the active destination on both bottom nav
  and navigation rail forms.
- Top-level transitions must prevent duplicate destination stacking.
- Existing deep links (`ndi://viewer/{sourceId}`, `ndi://output/{sourceId}`)
  remain valid and must still expose Home/Stream/View navigation controls.

## 4. Adaptive Layout Contract

- Phone layouts use Material 3 bottom navigation.
- Tablet layouts use Material 3 navigation rail.
- Layout profile selection depends on runtime width class/screen-width rules and
  must not alter functional navigation behavior.

## 5. Continuity and Restore Contract

- Launcher launch opens Home by default.
- Recents/task restore reopens last top-level destination.
- Stream continuity:
  - active output keeps running when navigating to Home or View
  - after process death, show last-known status and require explicit restart
- View continuity:
  - leaving View stops playback immediately
  - selected source remains highlighted on return/relaunch
  - autoplay is prohibited on return/relaunch

## 6. Observability Contract

Required non-sensitive event categories:

- top_level_destination_selected
- top_level_destination_reselected_noop
- top_level_navigation_failed
- home_dashboard_viewed
- home_action_open_stream
- home_action_open_view

Payload constraints:

- No raw media payloads.
- No personally identifiable information.
- Use destination IDs and anonymized reason/status codes only.

## 7. Permission and Security Contract

- No new dangerous permissions are allowed for this feature.
- Any new persisted field must be non-sensitive and local-only.
- Direct DB access from presentation layer is prohibited.

## 8. Release Validation Contract

- Unit tests and UI flow tests must cover Home->Stream, Home->View,
  Stream->View, View->Stream, and active-destination highlighting behavior.
- Validation must include launcher-entry default Home behavior and
  Recents-restore last destination behavior.
- Release validation must keep `verifyReleaseHardening` and
  `:app:assembleRelease` in scope before feature completion.

