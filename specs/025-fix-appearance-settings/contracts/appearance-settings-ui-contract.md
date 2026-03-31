# Contract: Appearance Settings UI

## Scope

Defines user-visible behavior and navigation contract for Appearance settings and Theme Editor integration.

## Inputs

- User selects one of:
  - `Light`
  - `Dark`
  - `System Default`
- User taps `Save` in settings.
- User taps `Color Theme` entry point in Appearance panel.

## Outputs

- Theme mode is persisted and globally applied.
- Selected mode is represented as current selection when settings reopens.
- Theme Editor route opens from Appearance panel.
- Accent selection updates persisted state and active app accent.

## Behavioral Contract

1. Appearance detail panel MUST contain theme mode controls and a color theme entry point in both compact and wide layouts.
2. Saving mode MUST apply immediately through app-wide theme coordinator behavior.
3. `System Default` MUST track device theme changes while app is active.
4. Color theme entry point MUST navigate using existing deep-link contract:
   - `ndi://theme-editor`
5. App MUST remain stable if Theme Editor navigation cannot be resolved (no crash).

## Persistence Contract

- Storage row key: `settings_preference.id = 1`
- Fields owned/used by this feature:
  - `themeMode`
  - `accentColorId`
  - `updatedAtEpochMillis`
- Save paths MUST preserve non-owned settings fields.

## Verification Contract

- Unit tests:
  - Theme mode save/apply propagation and persistence.
  - Appearance controls visibility and selected-state restoration.
- Playwright e2e:
  - Light/Dark: persisted selection + stable visual token.
  - System Default: device theme toggle follow-through.
  - Color theme entry point visibility and navigation.
  - Existing Playwright regression suite remains passing.
