# Research: Bottom Navigation Settings Access

**Feature**: 014-bottom-nav-settings  
**Date**: March 26, 2026

## Decision 1: Use dedicated Settings bottom navigation destination

- Decision: Add Settings as a first-class bottom navigation item (Home, Stream, View, Settings).
- Rationale: This directly satisfies clarified requirement A, removes duplicate settings entry paths, and aligns top-level navigation affordances with user expectation.
- Alternatives considered:
  - Keep top-right action and add bottom navigation entry: rejected because dual entry points reintroduce conflicting metaphors and higher maintenance.
  - Keep only top-right action: rejected because it does not satisfy clarified feature intent.

## Decision 2: Keep canonical navigation in Navigation Component with destination de-duplication

- Decision: Route Settings open/exit through existing app navigation helpers and enforce de-duplication semantics when selecting current destination.
- Rationale: Preserves single-activity architecture and avoids duplicate settings destinations during repeated taps.
- Alternatives considered:
  - Manual FragmentTransaction handling in screens: rejected because it violates established Navigation Component architecture.
  - Per-screen custom toggle logic: rejected due to inconsistent behavior and increased regression risk.

## Decision 3: Synchronize bottom-nav selected state from active destination state

- Decision: Treat current destination as source of truth and derive selected bottom-nav item from it, including deep-link and rotation restoration paths.
- Rationale: Prevents UI-state drift during rapid switching, process recreation, and deep-link starts.
- Alternatives considered:
  - Keep selected tab as standalone mutable UI state: rejected because it can desynchronize from nav back stack.

## Decision 4: Remove top-right settings affordance across in-scope surfaces

- Decision: Remove toolbar settings entry controls from source list, viewer, output, and settings surfaces.
- Rationale: Requirement FR-005 explicitly requires removal and avoids duplicate discoverability paths.
- Alternatives considered:
  - Hide affordance conditionally by destination: rejected because behavior remains harder to reason about and test.

## Decision 5: Enforce visual-change quality gates with Playwright

- Decision: Add/adjust emulator-run Playwright scenarios for settings entry/exit via bottom nav and run full regression suite in the same validation cycle.
- Rationale: Constitution mandates Playwright for visual behavior changes and continued regression confidence.
- Alternatives considered:
  - Espresso-only coverage: rejected by constitution unless Playwright infeasible with justification.
  - Unit-test-only validation: rejected because navigation visuals and selectors require end-to-end verification.

## Decision 6: Keep data and permissions unchanged

- Decision: Do not add new persistence entities, permissions, or background processing.
- Rationale: Feature scope is navigation affordance replacement only.
- Alternatives considered:
  - Persist last selected tab for this feature: rejected as out-of-scope and unnecessary for acceptance criteria.
