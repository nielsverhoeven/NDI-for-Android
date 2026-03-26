# Settings Gear Toggle UI Contract

**Feature**: 013-settings-gear-toggle  
**Date**: March 26, 2026  

## Scope

This contract applies to the following surfaces:

- Source list screen
- Viewer screen
- Output screen
- Settings screen

## User-Facing Contract

### Visibility

- Each in-scope surface MUST render a settings gear affordance in the top-right corner of the top app bar.
- The affordance MUST remain visible without opening an overflow menu.
- The affordance MUST use the existing settings label/content description semantics so accessibility tooling and Playwright selectors can discover it.

### Open Behavior

- From source list, viewer, or output, tapping the gear MUST navigate to `settingsFragment`.
- The resulting settings UI MUST become visible within 1 second under normal emulator conditions.

### Close Behavior

- While `settingsFragment` is the current destination, the top-right gear affordance on the settings screen MUST close settings and return the user to the immediately previous surface.
- Closing settings MUST not discard existing saved settings beyond normal unsaved-form behavior already defined by the current settings screen.

### Stability Rules

- Rapid repeated taps MUST NOT stack multiple settings destinations.
- Rotation while settings is open MUST preserve the settings destination and its visible gear affordance.
- Rotation while settings is closed MUST preserve the visible gear affordance on the current in-scope surface.

## Navigation Contract

- `NdiNavigation.settingsRequest()` remains the canonical open-settings route.
- `settingsFragment` remains the canonical settings destination id.
- “Toggle” semantics are implemented as:
  - open: navigate to `settingsFragment` when current destination is not `settingsFragment`
  - close: `popBackStack()` when current destination is `settingsFragment`

## Automation / Playwright Contract

- Playwright must be able to discover the settings affordance via accessible button text such as `Settings` or `Open settings`, or via a stable equivalent selector introduced during implementation.
- The following specs are the required acceptance automation entry points:
  - `testing/e2e/tests/settings-navigation-source-list.spec.ts`
  - `testing/e2e/tests/settings-navigation-viewer.spec.ts`
  - `testing/e2e/tests/settings-navigation-output.spec.ts`
- Existing regression suite coverage listed in `testing/e2e/tests/support/e2e-suite-classification.spec.ts` MUST also remain passing.

## Non-Goals

- Replacing the existing settings fragment with a bottom sheet or dialog
- Changing settings persistence or repository contracts
- Expanding settings entry behavior to unrelated screens outside the defined scope
