# Bottom Navigation Settings UI Contract

**Feature**: 014-bottom-nav-settings  
**Date**: March 26, 2026

## Scope

This contract applies to source list, viewer, output, and settings surfaces.

## User-Facing Contract

### Visibility and Entry

- Bottom navigation MUST expose Home, Stream, View, and Settings items.
- Settings MUST be reachable from Home, Stream, and View in one tap through bottom navigation.
- Top-right toolbar settings entry affordances MUST NOT be shown on source list, viewer, output, or settings surfaces.

### Exit Behavior

- From settings, selecting Home, Stream, or View in bottom navigation MUST navigate to that destination.
- Selected-state highlighting MUST match the currently visible destination.

### Stability Rules

- Re-selecting Settings while settings is visible MUST NOT create duplicate settings destinations.
- Rapid bottom-nav tab switching MUST keep selected state and visible destination synchronized.
- Device rotation MUST preserve visible destination and selected bottom-nav state.
- If app starts from deep link (viewer/output), settings remains reachable via bottom navigation.

## Navigation Contract

- Navigation remains single-activity using Navigation Component.
- Existing viewer and output deep links remain valid and unchanged.
- Settings route stays a destination screen, not modal.

## Automation Contract

- Playwright MUST cover:
  - enter settings from Home/Stream/View via bottom navigation
  - exit settings to Home/Stream/View via bottom navigation
  - no top-right settings affordance on in-scope surfaces
- Existing Playwright regression suite MUST run and remain passing in the same validation cycle.

## Non-Goals

- Introducing additional settings entry points outside bottom navigation.
- Changing settings persistence behavior or repository contracts.
- Adding background jobs, permissions, or unrelated navigation destinations.
