# Data Model: Fix Appearance Settings

## Entity: AppearancePreferenceSnapshot

- Description: Persisted user appearance preferences stored in `settings_preference` and mapped into `NdiSettingsSnapshot`.
- Fields:
  - `themeMode`: enum (`LIGHT`, `DARK`, `SYSTEM`), required.
  - `accentColorId`: string accent key (for example `accent_teal`), required.
  - `updatedAtEpochMillis`: long timestamp, required.
  - `developerModeEnabled`: boolean, required but not modified by this feature.
  - `discoveryServerInput`: nullable string, preserved but not owned by this feature.
- Relationships:
  - Mapped to/from `ThemePreference` in theme editor flow.
  - Consumed by `SettingsViewModel` and `AppThemeCoordinator` through repositories.
- Validation rules:
  - `themeMode` defaults to `SYSTEM` on missing/invalid persisted value.
  - `accentColorId` must normalize to a known theme accent value.
  - Save operations must preserve non-owned fields.

## Entity: AppearanceUiState

- Description: In-memory settings state representing pending and saved appearance values.
- Fields:
  - `themeMode`: currently selected mode in settings UI.
  - `isDirty`: true when `themeMode` or developer toggle differs from baseline.
  - `savedConfirmationVisible`: indicates successful save feedback.
- Relationships:
  - Produced by `SettingsViewModel`.
  - Rendered by `SettingsDetailRenderer` and `SettingsScreen`.
- Validation rules:
  - `isDirty` must flip true on mode change and false after successful save.
  - Re-entering settings must reflect persisted mode as selected.

## Entity: ThemePreferenceStream

- Description: Reactive stream used by `AppThemeCoordinator` to apply global night mode and active accent.
- Fields:
  - `themeMode`
  - `accentColorId`
- Relationships:
  - Source: theme repository backed by Room settings row.
  - Consumer: `AppThemeCoordinator` applying `AppCompatDelegate.setDefaultNightMode`.
- Validation rules:
  - Stream must emit for writes originating from both settings and theme editor save paths.

## State Transitions

1. Open Settings > Appearance:
   - Persisted snapshot -> `SettingsUiState.themeMode`.
2. Select Light/Dark/System:
   - `SettingsUiState.themeMode` updates.
   - `isDirty = true`.
3. Tap Save:
   - Updated snapshot persisted to Room.
   - Theme stream emits new preference.
   - Coordinator applies mode globally.
   - `isDirty = false`, save confirmation shown.
4. Navigate to Theme Editor and select accent:
   - Accent saved and normalized.
   - Theme stream emits new accent.
   - App theme overlay/coordinator state reflects update.
