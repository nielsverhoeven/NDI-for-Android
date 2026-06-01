# Data Model: Bottom Navigation Settings Access

**Feature**: 014-bottom-nav-settings  
**Date**: March 26, 2026

## Entity: NavigationDestinationState

Represents current navigation destination and selected bottom navigation state.

### Fields

- currentDestinationId: String (non-empty)
- selectedBottomNavItem: Enum {HOME, STREAM, VIEW, SETTINGS}
- previousTopLevelDestination: Enum {HOME, STREAM, VIEW, SETTINGS, NONE}
- launchedFromDeepLink: Boolean
- isSettingsVisible: Boolean

### Validation Rules

- selectedBottomNavItem MUST correspond to currentDestinationId top-level mapping.
- isSettingsVisible MUST be true only when currentDestinationId maps to settings destination.
- previousTopLevelDestination MUST NOT be NONE after first top-level navigation occurs.

### State Transitions

- NonSettingsToSettings:
  - Trigger: user taps SETTINGS in bottom nav.
  - Result: currentDestinationId -> settings; selectedBottomNavItem -> SETTINGS.
- SettingsToNonSettings:
  - Trigger: user taps HOME/STREAM/VIEW in bottom nav.
  - Result: currentDestinationId -> tapped destination; selectedBottomNavItem -> tapped item.
- ReSelectSettings:
  - Trigger: user taps SETTINGS while already on settings.
  - Result: no duplicate destination; state unchanged.
- RotationRestore:
  - Trigger: configuration change while any top-level destination visible.
  - Result: destination and selectedBottomNavItem restored consistently.
- DeepLinkStart:
  - Trigger: deep link into viewer/output.
  - Result: currentDestinationId -> deep-linked destination; selectedBottomNavItem maps accordingly; settings still reachable via SETTINGS.

## Entity: SettingsEntryAffordancePolicy

Defines canonical settings entry points for in-scope surfaces.

### Fields

- bottomNavEnabled: Boolean (must be true)
- topRightAffordanceEnabled: Boolean (must be false)
- inScopeSurfaces: Set {SOURCE_LIST, VIEWER, OUTPUT, SETTINGS}

### Validation Rules

- bottomNavEnabled MUST remain true across all in-scope surfaces.
- topRightAffordanceEnabled MUST remain false for all in-scope surfaces.

## Relationship Model

- NavigationDestinationState selectedBottomNavItem enforces SettingsEntryAffordancePolicy by providing the only user-facing settings entry path.
- SettingsEntryAffordancePolicy constrains UI rendering decisions for source list, viewer, output, and settings surfaces.
