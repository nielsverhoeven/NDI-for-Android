# Contract: Settings Theme Editor

## 1. Theme Editor Entry Contract

- Settings screen MUST expose a navigation entry to the theme editor surface.
- Entry availability MUST match settings availability across supported app flows.

## 2. Theme Mode Selection Contract

Inputs:

- `selectThemeMode(mode)` where `mode in {LIGHT, DARK, SYSTEM}`

Outputs:

- persisted `themeMode`
- app appearance update
- selected-state reflection in editor UI

Guarantees:

- Exactly one mode is active at any time.
- Selecting `SYSTEM` preserves and uses existing device-follow behavior.
- Mode selection persists across relaunch.

## 3. Accent Selection Contract

Inputs:

- `selectAccent(accentColorId)` where `accentColorId` belongs to curated palette

Outputs:

- persisted `accentColorId`
- accent visual update on user-visible accent surfaces
- selected-state reflection in editor UI

Guarantees:

- Palette size is 6-8 accessible options.
- Exactly one accent is active at any time.
- Accent selection persists across relaunch.

## 4. Persistence Contract

- Theme settings are stored through existing settings repository abstraction.
- Reads return valid values or safe defaults (`SYSTEM` + default accent).
- Invalid persisted values are normalized to defaults without app crash.

## 5. Testing and Quality Contract

- Unit tests MUST cover state transitions and persistence mapping.
- Instrumentation tests MUST cover settings UI selected-state behavior.
- Playwright e2e MUST cover mode switching, accent switching, and persistence check.
- Existing Playwright regression suite MUST execute and remain passing.

## 6. Security and Architecture Contract

- No new runtime permissions introduced.
- Theme behavior changes remain within MVVM and repository boundaries.
- No direct persistence calls from fragment/view layers.
