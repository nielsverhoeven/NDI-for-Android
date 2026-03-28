# Research: Three-Column Settings Layout

## Decision 1: Trigger rule for three-column mode

- Decision: Activate three-column settings layout using the app's existing wide-layout criteria, rather than a new standalone width/orientation rule.
- Rationale: This was clarified in the spec and keeps behavior consistent with current large-screen handling, minimizing regressions and duplicated breakpoint logic.
- Alternatives considered:
  - Always use landscape for three-column mode: rejected because narrow landscape devices can produce cramped layouts.
  - Introduce a new fixed threshold just for settings: rejected to avoid divergence from existing layout policy.

## Decision 2: Pane interaction model

- Decision: Use stable three-pane behavior where column 1 (main navigation) and column 2 (settings menu) remain visible while column 3 updates in place for selected category details.
- Rationale: Matches core user value of reduced navigation churn and direct settings manipulation.
- Alternatives considered:
  - Replace the full screen when category changes: rejected because it removes core three-pane benefit.
  - Use modal overlays for details: rejected as less discoverable and weaker for multitask-style settings workflows.

## Decision 3: Compact fallback behavior

- Decision: Keep existing compact settings presentation for non-wide layouts (including phone portrait), with compatibility-focused flow continuity.
- Rationale: Prevents usability regressions on constrained screens and aligns with feature scope.
- Alternatives considered:
  - Force three-pane everywhere: rejected due to density/accessibility risks on small viewports.
  - Add a brand-new compact navigation model: rejected because it exceeds requested scope.

## Decision 4: Context preservation across layout transitions

- Decision: Preserve the currently selected settings category when transitioning between wide and compact layouts whenever the target category still exists.
- Rationale: Avoids disorientation and unnecessary reselection during rotations or window-size changes.
- Alternatives considered:
  - Always reset to first category: rejected because it interrupts user intent.
  - Persist full scroll/cursor state for every control: rejected for complexity beyond current requirement.

## Decision 5: Accessibility and content-density handling

- Decision: Ensure the three columns remain readable under larger font scales with resilient truncation/wrapping and clear selected-state indicators.
- Rationale: The feature explicitly introduces dense UI; accessibility-safe rendering is necessary for acceptance.
- Alternatives considered:
  - Hard-fixed widths without adaptation: rejected due to overflow and clipping risk.
  - Collapse columns under moderate text scaling: rejected as it weakens predictable behavior in wide mode.

## Decision 6: Architecture and module boundaries

- Decision: Implement changes primarily in existing settings presentation paths while preserving Fragment -> ViewModel -> Repository flow and app-level navigation graph wiring.
- Rationale: Maintains constitution compliance and repository conventions from AGENTS guidance.
- Alternatives considered:
  - Introduce a new module for this layout only: rejected as unnecessary overhead for this scope.
  - Put selection/business logic in Fragment/UI layer: rejected due to MVVM violation risk.

## Decision 7: Validation and quality gates

- Decision: Use strict test-first path with unit tests for selection/state transitions and Playwright emulator e2e for visual behaviors, followed by full existing Playwright regression.
- Rationale: Required by constitution for UI changes and ensures broad regression safety.
- Alternatives considered:
  - Manual verification only: rejected as non-repeatable and insufficient.
  - Espresso-first for new visual coverage: rejected because repository e2e standard is Playwright.

## Decision 8: Preflight and environment-blocked reporting

- Decision: Execute prerequisite checks before emulator-driven validation and classify blocked gates separately from code failures with explicit unblocking actions.
- Rationale: Required by constitution principle XII and reduces false-negative debugging effort.
- Alternatives considered:
  - Run tests immediately without preflight: rejected due to known environment drift in emulator workflows.

## Resolved Clarifications

All planning-time unknowns for this feature are resolved. No `NEEDS CLARIFICATION` markers remain.
