# Research Findings: Settings Gear Toggle

**Feature**: 013-settings-gear-toggle  
**Date**: March 26, 2026  
**Researcher**: Spec Kit plan workflow

## Research Tasks & Findings

### Task 1: Determine the existing settings entry architecture

**Decision**: Reuse the existing `settingsFragment` destination and `NdiNavigation.settingsRequest()` deep link rather than introducing a new modal surface.

**Rationale**: Source list, viewer, and output screens already route `action_settings` to `ndi://settings`, and the nav graph already defines `settingsFragment` as the settings destination. Reusing this path preserves the single-activity navigation model and avoids duplicating the already implemented settings UI and persistence behavior.

**Alternatives considered**:

- Bottom sheet dialog: Rejected because it would bypass the current navigation contract and duplicate an existing settings feature surface.
- Activity-level dialog/overlay: Rejected because it would add another UI architecture pattern to a fragment-driven app.

### Task 2: Determine how to satisfy “always visible in the top right corner” and explicit gear/cog iconography

**Decision**: Promote each `action_settings` toolbar item from `showAsAction="ifRoom"` to `showAsAction="always"`, enforce gear/cog icon assets and labels across all in-scope surfaces, and add an equivalent top app bar gear action to the settings screen itself.

**Rationale**: The refined specification requires both persistent top-right visibility and explicit gear/cog iconography. Standardizing icon resources/content descriptions prevents accidental wrench/manage substitutions and keeps accessibility/automation stable. The settings screen currently has no top app bar, so adding a top app bar with the same gear affordance is the simplest way to keep the icon visible while settings is open.

**Alternatives considered**:

- Global activity toolbar: Rejected because the app currently uses fragment-owned top app bars.
- Floating overlay button: Rejected because it would break Material top app bar conventions and complicate layout handling.
- Wrench/manage icon variants: Rejected because the spec explicitly requires gear/cog iconography and forbids alternate settings metaphors for this affordance.

### Task 2b: Ensure a visible settings header/title while settings is open

**Decision**: Add a visible top app bar title on the settings screen (for example, "Settings") while preserving the top-right gear affordance.

**Rationale**: The updated requirements explicitly call for a visible settings header/title while open. A Material top app bar title is the most direct, consistent, and accessible way to satisfy this without introducing custom chrome.

**Alternatives considered**:

- Header text inside body content only: Rejected because it can scroll away and does not satisfy a stable top-level screen heading requirement.
- Icon-only toolbar with no title: Rejected because the spec explicitly requires a visible settings header/title.

### Task 3: Define the toggle behavior without violating navigation architecture

**Decision**: Model toggle behavior as `open = navigate to settingsFragment` and `close = popBackStack()` when the current destination is `settingsFragment`.

**Rationale**: The specification describes open/close semantics, but the codebase represents settings as a dedicated destination. Using the same gear affordance to navigate into settings from non-settings screens and pop back out from the settings screen satisfies the user-visible toggle expectation without introducing a parallel state container.

**Alternatives considered**:

- Replace navigation with an in-place menu panel: Rejected because it would require broader screen restructuring and conflicts with the existing settings destination.
- Keep back arrow on settings and treat only open as gear behavior: Rejected because the feature explicitly requires the same button to close when settings is open.

### Task 4: Handle rotation and rapid repeated taps safely

**Decision**: Derive the toggle state from the current navigation destination/back stack and guard against duplicate navigation events so repeated taps cannot stack multiple settings destinations.

**Rationale**: The edge cases call out rapid taps and rotation. Nav destination state already survives configuration changes better than ad hoc fragment Booleans. A single navigation helper or destination check can ensure the app no-ops on redundant “open” requests and pops exactly once on “close”.

**Alternatives considered**:

- Local fragment Boolean only: Rejected because it risks desynchronizing after rotation or process recreation.
- Persisting a settings-open flag in storage: Rejected because open/close is transient UI state, not persisted domain state.

### Task 5: Define test strategy for the visual toggle requirement

**Decision**: Convert the existing placeholder Playwright `settings-navigation-*` specs from expected-fail coverage to real emulator-run toggle tests, and require a full existing regression suite pass alongside them.

**Rationale**: The constitution and feature spec both require emulator Playwright coverage for visual changes and an explicit existing-suite regression run. The repo already has placeholder settings navigation specs plus a `test:pr:primary` path that bundles new-settings and existing-regression coverage.

**Alternatives considered**:

- Unit tests only: Rejected because visibility and toolbar interaction are visual behaviors that require end-to-end verification.
- Adding entirely new e2e specs without reusing placeholders: Rejected because the current placeholders already define the intended coverage entry points.

### Task 6: Confirm feature scope boundaries

**Decision**: Scope the feature to surfaces that already expose or host settings behavior: source list, viewer, output, and the settings screen itself.

**Rationale**: These are the concrete surfaces with existing settings entry points or the settings destination itself. Expanding the scope to unrelated screens such as the home dashboard would be a separate navigation/product decision not described in the feature spec.

**Alternatives considered**:

- Entire app chrome overhaul: Rejected as out of scope for the requested gear-toggle behavior.

