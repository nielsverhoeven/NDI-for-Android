# Contract: View Screen Controls Refinement

## 1. Source List Rendering Contract

### 1.1 SourceFilteringContract

Requirements:

- The rendered View source list must exclude entries representing the current device.
- Filtering is applied consistently on initial load and on every refresh result.
- If the only discovered source is the current device, the rendered list is empty.

### 1.2 SourceActionContract

Requirements:

- Each rendered source row must include exactly one primary action labeled "view stream".
- Only the "view stream" button can initiate viewer navigation for that source.
- Non-button row taps must not trigger navigation or output actions.

## 2. View Screen Action Surface Contract

### 2.1 OutputActionVisibilityContract

Requirements:

- The View screen must not expose a direct "start output" action/button.
- Output initiation remains routed through stream menu flow outside this screen.

## 3. Refresh Interaction Contract

### 3.1 RefreshPlacementContract

Requirements:

- Refresh button is presented in the bottom-left control area of the View screen.
- Loading indicator is rendered adjacent to the refresh button while refresh is active.

### 3.2 RefreshInFlightContract

Inputs:

- refreshRequested
- refreshSucceeded
- refreshFailed

Outputs:

- isRefreshing
- refreshEnabled
- showLoadingIndicator

Guarantees:

- On `refreshRequested`: `isRefreshing=true`, refresh button disabled, loading indicator visible.
- While refresh is active: currently visible source list remains visible.
- On `refreshSucceeded`: source list replaced atomically with filtered refreshed results, refresh button enabled, loading indicator hidden.
- On `refreshFailed`: source list remains unchanged, refresh button enabled, loading indicator hidden.

### 3.3 RefreshFailureFeedbackContract

Requirements:

- Refresh failure must display an inline non-blocking error message near refresh controls.
- Refresh failure feedback must not block source list interaction.
- Refresh failure feedback must not clear currently displayed list.

## 4. Navigation Contract

### 4.1 ViewToViewerNavigationContract

Requirements:

- Pressing source "view stream" action must navigate to viewer for that source identifier.
- No other tap target on source rows can emit viewer navigation.

## 5. Testing and Quality Contract

### 5.1 Unit and Integration Contract

Requirements:

- JUnit tests must cover source filtering, refresh in-flight state transitions, refresh failure state, and non-button tap inert behavior.

### 5.2 Visual E2E Contract

Requirements:

- Emulator-run Playwright coverage must verify:
  - current-device exclusion,
  - button-only navigation initiation,
  - no output-start action on View screen,
  - refresh placement and adjacent loading indicator,
  - disabled refresh during in-flight state,
  - inline non-blocking error on refresh failure with preserved list.
- Existing Playwright e2e suite must be executed and remain passing.

## 6. Security and Architecture Contract

Requirements:

- No new dangerous permissions are introduced.
- Presentation logic remains MVVM-compliant and repository mediated.
- No direct persistence or native SDK bridge access from UI layer.

## 7. Verification Notes

Status snapshot (2026-03-27):

- SourceFilteringContract: implemented in `SourceListViewModel` filtering (`device-screen:local`) with unit + instrumentation + Playwright scenario coverage authored.
- SourceActionContract: implemented with button-only `viewStreamButton` callback and inert row container; unit/instrumentation tests authored.
- OutputActionVisibilityContract: implemented by removing direct output action wiring and replacing row action label with "View Stream".
- Refresh contracts (3.1/3.2/3.3): implemented in `SourceListUiState` (`isRefreshing`, `refreshErrorMessage`) and refresh controls layout; tests authored for in-flight disablement and inline error behavior.
- Visual E2E Contract: feature specs are added to Playwright manifest; full suite execution still pending.
- Security/architecture constraints: no new permissions introduced; changes remain in presentation layer and existing repository boundaries.

Open validation blockers:

- Full unit and androidTest execution is currently blocked by pre-existing failures in unrelated viewer/settings test suites.
- Full Playwright regression and release hardening runs are still pending completion evidence.
