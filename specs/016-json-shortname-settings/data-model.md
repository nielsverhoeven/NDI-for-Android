# Data Model: Theme Editor Settings

## Entity: ThemePreference

- Purpose: Persisted user appearance preference state for the app.
- Fields:
  - themeMode (enum, required): `LIGHT`, `DARK`, `SYSTEM`
  - accentColorId (string, required): one of curated palette IDs
  - updatedAtEpochMillis (long, required)
- Validation rules:
  - `themeMode` must be one of the three supported values.
  - `accentColorId` must belong to the curated palette set.
  - `updatedAtEpochMillis` must be set on every successful save.

## Entity: ThemeModeOption

- Purpose: Selectable UI option for app appearance mode.
- Fields:
  - id (string, required): `LIGHT` | `DARK` | `SYSTEM`
  - displayLabel (string, required)
  - isSelected (boolean, required)
- Validation rules:
  - Exactly one option is selected at any time.

## Entity: AccentColorOption

- Purpose: Selectable UI option for accent color.
- Fields:
  - id (string, required)
  - displayLabel (string, required)
  - previewColorToken (string, required)
  - isSelected (boolean, required)
- Validation rules:
  - Curated set size is 6-8 options.
  - Exactly one option is selected at any time.

## Relationships

- `ThemePreference.themeMode` maps to one active `ThemeModeOption`.
- `ThemePreference.accentColorId` maps to one active `AccentColorOption`.
- Settings UI state is derived from persisted `ThemePreference` plus current device mode (for `SYSTEM`).

## State Transitions

- Initial load:
  - Load persisted `ThemePreference` from settings repository.
  - If missing/invalid, fallback to defaults (`SYSTEM`, default accent ID).

- Theme mode update:
  - User selects mode -> update state -> persist `themeMode` with existing `accentColorId` -> apply app-wide mode.

- Accent update:
  - User selects accent -> update state -> persist `accentColorId` with existing `themeMode` -> apply app-wide accent.

- Relaunch restore:
  - On app start, read persisted preference and apply before/at first render stage of host UI.

- System mode follow:
  - If `themeMode=SYSTEM`, app appearance tracks device dark/light state while retaining selected accent.
