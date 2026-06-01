# Contract: Three-Screen Navigation Repairs and Unified Version-Aware E2E

## 1. Navigation and Destination Contracts

### 1.1 TopLevelNavigationContract

Top-level destinations:

- HomeRoute
- StreamRoute
- ViewRoute

Behavioral requirements:

- Top-level navigation must render icons as Home=house, Stream=camera, View=screen.
- Exactly one top-level destination is selected at a time.
- Selected destination must match the currently visible top-level destination.
- Stream setup/control surfaces must keep Stream selected as active destination.

### 1.2 ViewRouteContract

Required route behavior:

- Selecting a source from View root must navigate to ViewerRoute(sourceId).
- View source selection must never navigate to Stream/output setup routes.
- Back from ViewerRoute returns to View root.
- Back from View root returns to HomeRoute.

## 2. ViewModel Contracts

### 2.1 TopLevelNavViewModel

Inputs:

- onDestinationSelected(destination)
- onBackPressed(currentDestination)
- onDeepLinkResolved(route)

Outputs:

- selectedDestination (HOME | STREAM | VIEW)
- destinationItems (with icon and selected state)
- navigationEvent

Guarantees:

- Highlight state is derived from one canonical destination state.
- No duplicate top-level destination stacking on repeated selections.
- Back transition policy for View flow is deterministic.

### 2.2 ViewFlowViewModel

Inputs:

- onViewSourceSelected(sourceId)
- onViewerBackPressed()
- onViewRootBackPressed()

Outputs:

- navigationEvent: OpenViewer(sourceId) | OpenHome

Guarantees:

- Source selection from View root always emits OpenViewer.
- Viewer back emits return to View root.
- View root back emits OpenHome.

## 3. E2E Runtime Version Branching Contract

### 3.1 DeviceVersionDetectionContract

Inputs:

- publisher serial
- receiver serial

Outputs:

- publisher version profile (sdkInt, majorVersion)
- receiver version profile (sdkInt, majorVersion)

Requirements:

- Version detection happens before any stream/view interaction steps.
- Both profiles are attached to test diagnostics.

### 3.2 SupportedWindowContract

Requirements:

- Support window is rolling latest five Android major versions evaluated at runtime.
- Mixed major versions are allowed when both are inside the support window.
- If either device is unsupported, test run fails immediately with non-zero outcome.

### 3.3 UnifiedSuiteBranchingContract

Requirements:

- One unified Playwright suite performs runtime version branching.
- Version-specific behavior is implemented via per-device flow branches, not separate top-level scripts.
- Consent branches must prioritize full-screen sharing path over one-app sharing.

### 3.4 TimingPolicyContract

Requirements:

- Intentional static delays in helper logic must be <= 1000ms.
- Asynchronous waits should use state polling and timeout logic rather than long fixed sleeps.

## 4. Observability Contract

Required diagnostics/events:

- android_version_detected (per role)
- version_support_window_evaluated
- unsupported_version_fail_fast
- consent_flow_variant_selected (per role)
- view_selection_opened_viewer
- view_back_to_root
- view_root_back_to_home

Payload constraints:

- No raw media payloads.
- No PII.
- Device roles are allowed as publisher/receiver labels.

## 5. Security and Permission Contract

- No new dangerous permissions are introduced.
- MediaProjection user-consent flow remains mandatory for screen-share capture.
- Presentation layer must not directly access persistence APIs.

## 6. Release Validation Contract

Validation must include:

- Unit/UI tests for routing, back behavior, icon mapping, and highlight correctness.
- Unified Playwright dual-emulator run proving per-device version detection and branching.
- Unsupported-version fail-fast behavior verification.
- Release hardening checks (`verifyReleaseHardening`) before completion.
