# Research: Mobile Settings Parity

## Decision 1: Reuse Existing Wide/Compact Layout Rule and Port Improvements to Phone-Specific Compact Presentation

- Decision: Keep the existing wide-layout criterion for tablet/wide contexts and implement parity by aligning compact-phone settings presentation behavior with the improved tablet feature set.
- Rationale: This avoids breaking established tablet behavior while directly addressing the identified gap on mobile scale.
- Alternatives considered: Replacing all layout rules with a new classifier (rejected: unnecessary risk and regression scope expansion).

## Decision 2: Preserve Existing Navigation and ViewModel Boundaries

- Decision: Keep navigation in the single-activity `main_nav_graph.xml` and keep presentation logic in ViewModels/Fragments without moving business logic into UI resources.
- Rationale: Required by constitution principles I and II; minimizes architectural churn.
- Alternatives considered: Introduce a new settings activity (rejected: violates existing navigation architecture and increases back-stack complexity).

## Decision 3: Keep Repository and Room Persistence Contracts Unchanged

- Decision: Reuse existing settings persistence pathways (repository mediated, Room-backed) and add regression coverage for state preservation.
- Rationale: Feature scope is UI parity, not data model redesign; preserving storage contracts reduces migration risk.
- Alternatives considered: Add new settings schema fields for mobile layout state (rejected: out of scope and not required for parity).

## Decision 4: Testing Strategy Uses TDD + Playwright Visual Parity on Required Device Matrix

- Decision: Define failing tests first (JUnit/Playwright), then implement minimal UI changes to pass. Required visual validation matrix: baseline phone + compact-height phone + tablet profile.
- Rationale: Constitution requires TDD and emulator-run Playwright for visual changes. Two phone profiles reduce false confidence from a single device shape.
- Alternatives considered: Manual QA only (rejected: insufficient regression protection); instrumentation-only visual verification (rejected: does not satisfy default Playwright e2e expectation for this repo).

## Decision 5: Preflight-First Validation with Explicit Blocked Classification

- Decision: Require `scripts/verify-android-prereqs.ps1` and `scripts/verify-e2e-dual-emulator-prereqs.ps1` before e2e gates, and classify failures as code failure vs environment blocker with unblock steps.
- Rationale: Constitution principle XII and existing repo workflow prioritize deterministic environment checks.
- Alternatives considered: Run e2e directly and debug failures ad hoc (rejected: causes ambiguous failures and repeated CI churn).

## Decision 6: Material 3 and Accessibility Guardrails Are Mandatory for Phone Layout Changes

- Decision: Any compact-phone layout adjustments must preserve Material 3 semantics and accessibility readability at increased text scale.
- Rationale: Required by constitution principle V and feature edge-case expectations for clipped/overlapping controls.
- Alternatives considered: Pixel-perfect parity with tablet spacing (rejected: unsuitable for constrained phone viewport and harms usability).
