# Research: Fix Appearance Settings

## Decision 1: Make theme preference observation reactive across all save paths

- Decision: Ensure the stream consumed by `AppThemeCoordinator` emits when appearance settings are saved from `SettingsViewModel` (not only when saved from Theme Editor).
- Rationale: Current behavior relies on `ThemeEditorRepositoryImpl.observeThemePreference()`, which only updates its internal `state` on `saveThemePreference()`. `SettingsViewModel.onSaveSettings()` persists via `NdiSettingsRepository`, so the coordinator does not receive an update and Light/Dark/System appears broken.
- Alternatives considered:
  - Add one-off callback from settings screen to coordinator: rejected because it bypasses repository boundaries and increases UI coupling.
  - Force app restart after save: rejected as poor UX and does not satisfy immediate-apply requirements.
  - Use polling for DB changes: rejected due to complexity and battery impact.

## Decision 2: Keep a single source of truth in `settings_preference`

- Decision: Continue using `settings_preference` (Room) as the single persisted source for `themeMode` and `accentColorId`, with both settings and theme-editor paths preserving fields they do not own.
- Rationale: The table already stores both values and is shared by `NdiSettingsRepositoryImpl` and `ThemeEditorRepositoryImpl`. Preserving this model avoids migration risk and keeps compatibility with existing data.
- Alternatives considered:
  - Split theme mode and accent into separate tables: rejected as unnecessary for scope and would require migration/testing overhead.
  - Move to DataStore for this fix: rejected as out of scope for a bug-fix feature.

## Decision 3: Restore color theme access from Appearance detail panel in both layouts

- Decision: The Appearance detail panel must expose a visible entry point to Theme Editor in compact and wide settings layouts.
- Rationale: Regression removed user access to color theme changes from settings. The existing navigation contract (`ndi://theme-editor`) is already in place and should be reused.
- Alternatives considered:
  - Add color controls inline inside settings: rejected because Theme Editor already exists and would duplicate UX.
  - Surface color theme only in compact mode: rejected because spec requires parity across compact and wide layouts.

## Decision 4: Validate theme mode behavior with hybrid e2e assertions

- Decision: For Light and Dark, verify both persisted selection and one stable visual token; for System Default, toggle device theme and verify follow-system behavior.
- Rationale: Pure visual assertions are flaky; pure persistence checks can miss runtime application failures. Hybrid validation balances confidence and reliability.
- Alternatives considered:
  - Persisted state only: rejected because it cannot prove UI actually switched.
  - Visual token only: rejected because it can be brittle across rendering differences.

## Decision 5: Use existing Android/Playwright validation workflow and preflight gating

- Decision: Reuse preflight-first workflow (`scripts/verify-android-prereqs.ps1`, e2e manifest/profile scripts) and include full Playwright regression execution.
- Rationale: Constitution requires emulator preflight checks, explicit environment-blocked handling, and visual-regression coverage for UI changes.
- Alternatives considered:
  - Run only new appearance tests: rejected because constitution requires existing Playwright suite to remain passing.
  - Skip preflight in local runs: rejected because this repository treats environment readiness as a mandatory gate.
