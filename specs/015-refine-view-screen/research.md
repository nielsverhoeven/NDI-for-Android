# Phase 0 Research: Refine View Screen Controls

## Decision 1: Exclude current device from selectable view sources

- Decision: Filter out source entries mapped to the local app/device identity before rendering the list on the View screen.
- Rationale: Prevents invalid self-view selection and aligns with FR-001 and SC-001.
- Alternatives considered: Show self-source but disable button (rejected because still creates visual noise/confusion), show with warning badge (rejected as extra complexity without user value).

## Decision 2: Enforce button-only source activation

- Decision: Restrict stream opening to explicit "view stream" button press per source row; non-button row taps are inert.
- Rationale: Prevents accidental navigation and provides a clear interaction target.
- Alternatives considered: Entire row clickable with secondary action button (rejected due to accidental taps), long-press to open stream (rejected for discoverability).

## Decision 3: Remove output-start action from the View screen

- Decision: View screen does not expose a start-output action; output initiation remains available via stream menu flow only.
- Rationale: Keeps screen purpose focused and respects existing stream/output separation.
- Alternatives considered: Keep compact output shortcut on View screen (rejected as scope overlap and user confusion).

## Decision 4: Refresh control placement and in-flight behavior

- Decision: Place refresh control in bottom-left area; show loading icon adjacent to it while refresh runs; disable refresh action during in-flight refresh.
- Rationale: Matches clarified UX expectations and avoids duplicate refresh requests.
- Alternatives considered: Top app-bar refresh location (rejected by requirement), allow repeated refresh taps with debounce (rejected as ambiguous state handling).

## Decision 5: Keep visible list while refresh is in progress

- Decision: Preserve currently displayed source list during refresh and replace list only when refreshed data arrives.
- Rationale: Avoids flicker/context loss and supports continuity during refresh.
- Alternatives considered: Clear list immediately on refresh (rejected by clarification), dim and block list interactions globally (rejected as unnecessary interaction lock for this scope).

## Decision 6: Refresh failure feedback model

- Decision: On refresh failure, show inline non-blocking error message near refresh controls and keep current list visible.
- Rationale: Communicates error without disrupting user flow or removing known-good list state.
- Alternatives considered: Full-screen blocking error state (rejected as disruptive), toast/snackbar-only feedback (rejected due to transient visibility and weaker association with refresh control).

## Decision 7: Test strategy for visual behavior changes

- Decision: Use failing-first JUnit unit tests for ViewModel/UI state transitions and Playwright emulator e2e tests for visual interaction/placement behaviors; run full existing Playwright regression suite.
- Rationale: Satisfies constitution TDD and visual change quality gates.
- Alternatives considered: Unit tests only (rejected because placement/click-target behavior requires end-to-end verification), instrumentation-only without Playwright (rejected by project policy).

## Clarification Resolution Status

All identified behavior clarifications are resolved and encoded in specification and research decisions.
No `NEEDS CLARIFICATION` markers remain.
