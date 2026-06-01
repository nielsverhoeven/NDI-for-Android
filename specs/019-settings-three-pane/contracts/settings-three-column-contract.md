# Contract: Three-Column Settings Layout

## 1. Layout Mode Contract

- Three-column settings mode MUST be enabled only when the app's existing wide-layout criteria are met.
- Compact settings mode MUST be used when wide-layout criteria are not met.
- Layout transitions MUST preserve selected settings category context when that category still exists.

## 2. ViewModel Contract

### 2.1 SettingsLayoutViewModel (or equivalent existing ViewModel surface)

Inputs:

- onSettingsScreenVisible()
- onLayoutContextChanged(meetsWideCriteria, orientation, widthClass)
- onMainNavigationSelected(destination)
- onSettingsCategorySelected(categoryId)
- onRetryDetailLoad()

Outputs (state):

- layoutMode: `THREE_COLUMN` | `COMPACT`
- mainNavigationItems: list of Home, Stream, View, Settings with selected state
- settingsCategories: ordered list with one selected category when available
- selectedCategoryId
- settingsDetailState (groups or empty-state message)
- transientErrorMessage

Guarantees:

- In `THREE_COLUMN` mode, selecting a category updates detail state in-place without removing main navigation or category columns.
- Main navigation selection triggers app navigation while preserving consistent selected-item feedback.
- When selected category has no adjustable controls, detail state emits a clear empty-state message.

## 3. UI Contract

### 3.1 Three-column Settings Workspace

Required columns (left to right):

- Column 1: Main navigation with Home, Stream, View, Settings entries.
- Column 2: Settings menu categories with visible selected state.
- Column 3: Adjustable settings controls for the selected category, or an explicit empty state.

Required behaviors:

- Category selection in column 2 MUST only refresh column 3 content.
- Active selected category MUST be visually distinct.
- UI MUST remain usable under larger text scales without clipped critical controls.

### 3.2 Compact Fallback

- On non-wide layouts, settings MUST use the existing compact presentation.
- If the user rotates or changes window size, category context MUST be restored when supported by the destination layout.

## 4. Navigation Contract

- Feature continues using single-activity Navigation Component patterns.
- Main navigation actions from column 1 MUST route through existing navigation graph wiring.
- Returning to settings in a wide layout MUST render the three-column workspace.

## 5. Persistence and Repository Contract

- Existing settings read/write repository boundaries MUST remain intact.
- No direct Fragment/UI access to Room, DAO, or persistence APIs.
- This feature MUST NOT require new schema entities; it reuses current settings definitions and persistence behavior.

## 6. Validation Contract

- Preflight command before emulator e2e:
  - `scripts/verify-android-prereqs.ps1`
- Playwright emulator tests MUST cover:
  - opening settings in wide layout and asserting all three columns
  - selecting multiple settings categories and asserting in-place detail updates
  - switching to compact layout and confirming fallback behavior with preserved context when possible
- Existing Playwright regression suite MUST be run and remain passing.
- Validation results MUST classify failures as code defect vs environment blocker, with explicit unblock commands.
