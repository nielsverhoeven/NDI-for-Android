# Data Model: Settings Gear Toggle

**Feature**: 013-settings-gear-toggle  
**Date**: March 26, 2026  

## Entities

### 1. SettingsToggleSurface

- Purpose: Identifies which UI surface is dispatching the settings toggle action.
- Fields:
  - `surfaceId`: Enum `SOURCE_LIST | VIEWER | OUTPUT | SETTINGS`
  - `hasTopAppBar`: Boolean, always `true` for in-scope surfaces after implementation
  - `settingsActionVisible`: Boolean, must evaluate to `true` for all in-scope surfaces
  - `iconography`: Enum `GEAR_OR_COG` (no wrench/manage variants)
  - `settingsHeaderVisible`: Boolean, required `true` when `surfaceId == SETTINGS`

### 2. SettingsVisibilityState

- Purpose: Represents whether the app is currently showing the settings destination.
- Fields:
  - `currentDestinationId`: nav destination id
  - `isSettingsOpen`: derived Boolean where `currentDestinationId == settingsDestinationId`
  - `previousDestinationId`: optional nav destination id used to pop back to the originating surface

### 3. SettingsToggleIntent

- Purpose: Models a user-initiated gear press.
- Fields:
  - `originSurface`: `SettingsToggleSurface.surfaceId`
  - `timestamp`: transient event timestamp for ordering/debouncing if needed
  - `requestedAction`: Enum `OPEN | CLOSE`

## Relationships

- `SettingsToggleSurface` emits a `SettingsToggleIntent` when the user taps the gear affordance.
- `SettingsVisibilityState` determines whether the next `SettingsToggleIntent` resolves to navigation into `settingsFragment` or to a back-stack pop from `settingsFragment`.
- No persisted domain models or repository contracts change for this feature.

## Validation Rules

- The settings action must remain visible on every in-scope surface top app bar.
- The settings action iconography must be gear/cog on every in-scope surface; wrench/manage iconography is invalid.
- When `isSettingsOpen == false`, a gear tap must resolve to `requestedAction = OPEN` and navigate to `settingsFragment` exactly once.
- When `isSettingsOpen == true`, a gear tap must resolve to `requestedAction = CLOSE` and pop back to the previous surface exactly once.
- Repeated taps must not create duplicate `settingsFragment` entries on the back stack.
- Rotation/configuration change must preserve the correct `SettingsVisibilityState` as derived from the nav destination rather than a stale local Boolean.
- While `isSettingsOpen == true`, the settings surface must show a visible settings header/title and keep the top-right gear visible/tappable.

## State Transitions

1. `ClosedOnSurface(surface != SETTINGS)` -> gear tap -> `OpenOnSettings`
2. `OpenOnSettings` -> gear tap -> `ClosedOnPreviousSurface`
3. `OpenOnSettings` -> rotation -> `OpenOnSettings` (derived from restored nav destination)
4. `ClosedOnSurface` -> rapid repeated taps -> `OpenOnSettings` with no duplicate stack entries

## Persistence Impact

- None. Existing settings values remain in the current Room-backed settings feature and are out of scope for this UI-only interaction change.
