# Research Findings: Settings Gear Toggle

**Feature**: 013-settings-gear-toggle  
**Date**: March 23, 2026  
**Researcher**: speckit.plan

## Research Tasks & Findings

### Task 1: Research current implementation of settings menu access in the app

**Decision**: The current settings access is via a menu item in the top app bar overflow menu, with a gear icon, that navigates to a settings fragment.

**Rationale**: Code analysis shows menu XML with action_settings using @android:drawable/ic_menu_manage (gear icon), and tests expect navigation to settingsFragment. Release notes indicate navigation-based settings screen.

**Alternatives Considered**: 
- Modal dialog: Rejected because tests and navigation code indicate fragment navigation.
- Bottom sheet: Not currently implemented, but could be an alternative for the feature.

### Task 2: Research the UI structure of the app, especially how top app bars and menus are used

**Decision**: The app uses a single-activity architecture with Navigation Component. Each main screen (SourceList, Viewer, Output) is a fragment with its own TopAppBar containing a menu.

**Rationale**: Code shows Fragment classes with TopAppBar views, inflating menus like source_list_menu.xml. Single-activity confirmed by AGENTS.md.

**Alternatives Considered**: 
- Activity-level top bar: Not used, each fragment manages its own.
- Compose: App uses View binding, not Compose.

### Task 3: Research how the settings menu is currently implemented

**Decision**: Settings is implemented as a fragment destination in the navigation graph, accessed via deep link "ndi://settings".

**Rationale**: Navigation tests expect settingsRequest() returning NavDeepLinkRequest with "ndi://settings", and settingsDestinationId() returning R.id.settingsFragment.

**Alternatives Considered**: 
- Modal implementation: Not found in current code, but the feature spec suggests modal behavior (open/close vs navigate).

### Task 4: Research best practices for always-visible toggle buttons in Android apps

**Decision**: Use TopAppBar action with showAsAction="always" for guaranteed visibility in the top right.

**Rationale**: Material Design guidelines recommend toolbar actions for primary actions. showAsAction="always" ensures visibility regardless of screen size.

**Alternatives Considered**: 
- FloatingActionButton: Not standard for settings, typically for primary actions.
- Custom overlay view: More complex, violates standard Android patterns.

### Task 5: Research Material 3 guidelines for settings access and gear icons

**Decision**: Use gear icon (Icons.Filled.Settings) in an IconButton for settings access, following Material 3 iconography.

**Rationale**: Material 3 specifies gear/settings icon for settings actions. IconButton provides proper touch targets and states.

**Alternatives Considered**: 
- Custom icon: Not recommended, standard icons improve recognition.
- Text button: Less compact, not standard for toolbar actions.

### Task 6: Research implementation approach for toggle behavior

**Decision**: Change the menu item behavior from navigation to toggling a modal bottom sheet for settings.

**Rationale**: Feature spec requires "open/close" behavior, not navigation. BottomSheetDialogFragment aligns with Material 3 for settings panels.

**Alternatives Considered**: 
- AlertDialog: Less flexible for settings content.
- Navigation with back handling: Doesn't match "toggle" requirement.