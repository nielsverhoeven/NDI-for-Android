# Data Model: Mobile Settings Parity

## Overview

This feature primarily changes presentation behavior across form factors. It reuses existing persisted settings entities while introducing explicit UI-state modeling for parity validation.

## Entities

### 1. SettingsSection

- Purpose: Represents a settings category shown in the settings menu.
- Fields:
  - `id` (string, stable section identifier)
  - `title` (string, localized label)
  - `orderIndex` (int, deterministic ordering)
  - `isAvailable` (boolean, runtime visibility)
  - `isSelected` (boolean, current selection state)
- Relationships:
  - One `SettingsSection` can contain zero or more `SettingsOption` items.

Validation rules:

- `id` must be non-empty and unique in the section list.
- Exactly one selectable section should be selected when sections are available.
- Hidden/unavailable sections are not selectable.

### 2. SettingsOption

- Purpose: Represents an adjustable preference in a section.
- Fields:
  - `id` (string, stable option identifier)
  - `label` (string, localized display label)
  - `valueType` (enum: toggle, selection, action, text)
  - `currentValue` (typed value serialized by existing settings model)
  - `isEnabled` (boolean)
  - `validationState` (enum: valid, warning, invalid)
- Relationships:
  - Belongs to one `SettingsSection`.

Validation rules:

- Disabled options must not trigger state mutations.
- Display text must remain readable at phone scale and larger text settings.
- Invalid input states must surface user-visible feedback without blocking unrelated options.

### 3. FormFactorViewContext

- Purpose: Describes runtime layout context used to choose settings presentation behavior.
- Fields:
  - `deviceClass` (enum: phone, tablet)
  - `viewportClass` (enum: compact, wide)
  - `orientation` (enum: portrait, landscape)
  - `profileId` (enum: phone-baseline, phone-compact-height, tablet-reference)
- Relationships:
  - Drives layout mode for settings workspace rendering.

Validation rules:

- Phone compact-height profile must not hide required settings actions.
- Orientation changes must preserve selected section context when still available.

## State Transitions

### Settings workspace lifecycle

1. `Closed` -> `MenuVisible`
   - Trigger: User opens settings.
   - Guarantee: A valid section list is rendered for current `FormFactorViewContext`.

2. `MenuVisible` -> `SectionDetailVisible`
   - Trigger: User selects a settings section.
   - Guarantee: Detail panel updates to selected section without clipping or overlap.

3. `SectionDetailVisible` -> `SectionDetailVisible` (orientation/form-factor transition)
   - Trigger: Device rotates or layout class changes.
   - Guarantee: Selected section is preserved when possible; otherwise fallback selection is explicit.

4. `SectionDetailVisible` -> `Closed`
   - Trigger: User navigates away from settings.
   - Guarantee: Persisted settings values remain consistent with existing repository save behavior.

## Persistence Impact

- No new database tables or columns.
- Existing settings preference persistence remains authoritative.
- Regression checks must confirm no non-owned settings fields are lost during save operations.
