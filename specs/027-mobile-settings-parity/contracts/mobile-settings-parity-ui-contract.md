# Contract: Mobile Settings Parity UI

## 1. Scope

Defines required behavior for phone-scale settings parity with existing tablet improvements.

## 2. Layout and Navigation Contract

1. Settings must continue to use single-activity Navigation Component flows.
2. Phone presentation must expose the same improved settings sections available on tablet reference behavior.
3. Tablet/wide behavior remains unchanged as baseline reference.
4. On phone portrait and landscape, core settings controls remain reachable and readable without clipped interactive elements.

## 3. Interaction Contract

### 3.1 Section Access

- Opening settings on phone must render all in-scope sections.
- Selecting a section must open its details and preserve selected state indication.
- Navigating section-to-section must not require unsupported gestures.

### 3.2 Orientation Handling

- Rotating between portrait and landscape must keep settings usable.
- Selected section context must be preserved when supported in target layout.
- If preservation is not possible, fallback selection must be explicit and non-crashing.

### 3.3 State Persistence Visibility

- Saved settings values must remain visible when returning to settings on phone.
- Save operations must continue through existing repository-mediated paths.

## 4. Architecture and Data Boundary Contract

1. ViewModels hold presentation state and interaction decisions (MVVM-only logic).
2. Fragments/layout resources remain rendering and event-dispatch layers.
3. No direct UI-layer Room/DAO/persistence calls are permitted.
4. No schema changes are introduced by this feature.

## 5. Validation Contract

### 5.1 Required Preflight

- `scripts/verify-android-prereqs.ps1`
- `scripts/verify-e2e-dual-emulator-prereqs.ps1`

### 5.2 Required Emulator Matrix

- Phone baseline profile.
- Phone compact-height profile.
- Tablet reference profile.

### 5.3 Required Automated Coverage

- Playwright scenarios for:
  - opening settings and confirming section visibility on both required phone profiles,
  - section selection and detail interaction on phone,
  - portrait/landscape orientation continuity on phone,
  - parity confirmation against tablet reference flow.
- Existing Playwright regression suite must be executed and stay passing.
- Unit regression tests must protect state-preservation behavior for settings save paths touched by UI changes.

### 5.4 Failure Classification

- Validation outcomes must be categorized as:
  - `code-failure` for feature defects,
  - `blocked-environment` for preflight/emulator/tooling blockers.
- Blocked outcomes must include reproducible evidence and explicit unblocking command/action.
