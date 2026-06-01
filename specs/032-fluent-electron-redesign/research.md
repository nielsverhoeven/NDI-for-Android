# Research: Fluent + Electron UX Redesign

**Branch**: `032-fluent-electron-redesign` | **Date**: 2026-04-27

## Decision 1: Use a tokenized Fluent + Electron baseline mapped onto current Android UI stack

- Decision: Define a canonical token set (typography, spacing, color role, elevation, interaction states) and apply it through existing theme/style resources and presentation components, with explicit mapping notes where Material defaults are retained.
- Rationale: Satisfies FR-001/FR-002/FR-018 without forcing disruptive dependency replacement.
- Alternatives considered: Full design-system dependency replacement in one pass (rejected: high regression risk), ad-hoc per-screen restyling (rejected: inconsistent outcomes and hard-to-verify compliance).

## Decision 2: Roll out redesign by complete user flow slices

- Decision: Implement and ship redesign by complete flows (shell + Source List flow, Viewer flow, Output flow, Settings flow) and prohibit mixed legacy/redesigned UI within a shipped in-scope flow.
- Rationale: Directly implements FR-019 and clarification decision for phased rollout while controlling release risk.
- Alternatives considered: Big-bang all-screen rollout (rejected: elevated integration risk), shell-only rollout first (rejected: incomplete user-value delivery and potential inconsistency).

## Decision 3: Preserve behavior contracts and persistence semantics as invariants

- Decision: Treat discovery routing, playback/output logic, and settings persistence as behavior invariants; redesign may alter interaction affordances but not functional outcomes unless explicitly specified.
- Rationale: Implements FR-004/FR-005/FR-007 and reduces accidental regressions in critical NDI workflows.
- Alternatives considered: Bundling behavior refactors with visual work (rejected: confounds validation and violates scope discipline).

## Decision 4: Standardize redesign state patterns across loading/success/empty/error

- Decision: Define and apply one state-pattern contract per in-scope screen for loading, success, empty, and error UI semantics, including recovery action prominence.
- Rationale: Implements FR-006 and strengthens SC-001 consistency and user predictability.
- Alternatives considered: Reusing each screen's existing state rendering independently (rejected: inconsistent visual language and messaging hierarchy).

## Decision 5: Accessibility and adaptive layout are first-class release gates

- Decision: Validate redesign across phone/tablet, orientation changes, and increased text scale with explicit pass/fail criteria for readability, focusability, and reachable primary actions.
- Rationale: Implements FR-015/FR-016 and SC-005.
- Alternatives considered: Accessibility as post-implementation polish (rejected: high risk of late rework and constitution non-compliance).

## Decision 6: Evidence-first validation model under test-results

- Decision: Store per-screen Fluent + Electron compliance checklists and traceable test evidence links in feature-scoped `test-results` artifacts, alongside Playwright outputs and blocker classification.
- Rationale: Implements FR-010/FR-010a/FR-012 and supports auditable merge decisions.
- Alternatives considered: PR-comment-only evidence (rejected: non-durable and hard to audit), screenshot-only evidence without checklist linkage (rejected: low traceability).

## Decision 7: Regression-first testing strategy for visual redesign

- Decision: Add failing tests first for changed user-visible contracts, run targeted Playwright coverage for redesigned flows, then execute full existing Playwright regression suite before sign-off.
- Rationale: Implements FR-008/FR-009/FR-013/FR-014 and aligns with constitution TDD/regression policy.
- Alternatives considered: Updating or deleting failing legacy tests first (rejected: violates regression-protection requirement), manual validation only (rejected: insufficient confidence for broad UX changes).
