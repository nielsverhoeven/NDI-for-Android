# Data Model: Three-Column Settings Layout

## Entity: SettingsLayoutContext

- Purpose: Represents active layout mode and eligibility for three-pane rendering.
- Fields:
  - mode: `THREE_COLUMN` | `COMPACT`
  - meetsWideLayoutCriteria: Boolean
  - orientation: `PORTRAIT` | `LANDSCAPE`
  - widthClass: String (existing app classification)
  - lastTransitionEpochMillis: Long
- Validation rules:
  - `mode` must be `THREE_COLUMN` only when `meetsWideLayoutCriteria` is true.
  - Transition timestamps must update on mode change.

## Entity: MainNavigationState

- Purpose: Represents column-1 navigation entries and current active destination.
- Fields:
  - items: List<MainNavigationItem>
  - selectedDestination: `HOME` | `STREAM` | `VIEW` | `SETTINGS`
- Validation rules:
  - `items` must include all four required destinations.
  - Exactly one destination may be selected at a time.

## Entity: MainNavigationItem

- Purpose: Represents one navigation item in column 1.
- Fields:
  - destination: `HOME` | `STREAM` | `VIEW` | `SETTINGS`
  - label: String
  - isSelected: Boolean
  - isEnabled: Boolean
- Validation rules:
  - `isSelected` true for exactly one item.

## Entity: SettingsCategoryState

- Purpose: Represents column-2 menu categories and selected category context.
- Fields:
  - categories: List<SettingsCategory>
  - selectedCategoryId: String?
  - selectionSource: `DEFAULT` | `USER_TAP` | `RESTORED`
- Validation rules:
  - `selectedCategoryId` must be null only when `categories` is empty.
  - If non-null, `selectedCategoryId` must exist in `categories`.

## Entity: SettingsCategory

- Purpose: Represents one selectable settings category menu item.
- Fields:
  - id: String
  - title: String
  - subtitle: String?
  - isSelected: Boolean
  - hasAdjustableOptions: Boolean
- Validation rules:
  - `id` must be unique within category list.

## Entity: SettingsDetailState

- Purpose: Represents column-3 detail rendering state for currently selected category.
- Fields:
  - selectedCategoryId: String?
  - groups: List<SettingsDetailGroup>
  - emptyStateMessage: String?
  - isEditable: Boolean
- Validation rules:
  - If selected category has no options, `groups` is empty and `emptyStateMessage` is non-null.
  - If selected category has options, `groups` is non-empty and `emptyStateMessage` is null.

## Entity: SettingsDetailGroup

- Purpose: Represents a related set of adjustable settings controls.
- Fields:
  - id: String
  - title: String
  - controls: List<String>
- Validation rules:
  - Group IDs must be unique per selected category.

## Relationships

- `SettingsLayoutContext` determines whether three-pane rendering is active.
- `MainNavigationState` is rendered in column 1 for `THREE_COLUMN` mode.
- `SettingsCategoryState` drives selected category in column 2.
- `SettingsDetailState` is derived from `SettingsCategoryState.selectedCategoryId` and existing settings definitions.

## State Transitions

- `COMPACT -> THREE_COLUMN`: triggered when existing wide-layout criteria become true; selected category is restored when possible.
- `THREE_COLUMN -> COMPACT`: triggered when criteria become false; active category context is retained for compact flow when possible.
- Category selection transition: user selects a category in column 2, then detail content updates in column 3 without replacing columns 1 and 2.
- Empty-state transition: when selected category has zero adjustable options, details show explicit empty-state feedback.
