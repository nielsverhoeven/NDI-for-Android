# Research: Fluent Button Radius Alignment

**Branch**: `033-fluent-button-radius` | **Date**: 2026-04-27

## Decision 1: Enforce a single button corner profile through shared style tokens

- Decision: Define one canonical less-rounded corner radius profile for in-scope button components and route all included button variants to that profile via shared style/theme resources.
- Rationale: Ensures strict visual consistency required by the spec while minimizing per-layout drift.
- Alternatives considered: Per-screen manual radius overrides (rejected: high inconsistency risk), role-specific radius mapping (rejected: conflicts with strict uniform shape decision).

## Decision 2: Keep changes visual-only and behavior-neutral

- Decision: Restrict implementation to styling/resource-level updates and avoid touching button click handlers, navigation wiring, and ViewModel state transitions.
- Rationale: Aligns with clarified behavior policy and reduces regression risk.
- Alternatives considered: Bundling minor interaction refinements (rejected: violates visuals-only requirement), refactoring action wiring during redesign (rejected: unnecessary risk).

## Decision 3: Validate with focused flow coverage + full Playwright regression

- Decision: Add/adjust Playwright checks for button shape consistency in in-scope flows, then run the existing regression profile and preserve pass/fail/blocked evidence in feature-scoped test-results artifacts.
- Rationale: Provides proof of compliance and protects existing behavior.
- Alternatives considered: Screenshot-only validation (rejected: insufficient behavioral confidence), unit-only validation (rejected: misses integrated visual flow behavior).

## Decision 4: Explicitly guard edge-state rendering

- Decision: Include validation for disabled, focus, and dense-layout button rendering in both dark and light themes.
- Rationale: Button shape regressions often appear in non-default states; this closes that gap.
- Alternatives considered: Default-state-only checks (rejected: incomplete quality gate).

## Decision 5: Preserve module boundaries and avoid domain/data impact

- Decision: Keep implementation in app/presentation style and layout layers; no repository, domain contract, or persistence-model changes.
- Rationale: Satisfies constitution architecture principles and keeps feature scope constrained.
- Alternatives considered: Cross-module API abstraction for button geometry (rejected: unnecessary for current scoped change).
