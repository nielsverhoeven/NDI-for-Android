# Research: Rebuild Android E2E Suite

## Decision 1: Keep Playwright as the canonical e2e framework for Android emulator flows

- Decision: Retain Playwright-based Android e2e execution and rebuild the suite within [testing/e2e](../../testing/e2e) rather than migrating to Espresso/UIAutomator for this feature.
- Rationale: Constitution requires end-to-end flows to default to Playwright, and the repository already has mature Playwright harness assets (config, scripts, CI workflows, suite manifests).
- Alternatives considered:
  - Migrate all e2e to Espresso/UIAutomator now: rejected due to constitution conflict and high migration risk.
  - Hybrid immediate split (Playwright + new instrumentation e2e): rejected as unnecessary complexity for this rebuild objective.

## Decision 2: Use suite classification and manifest-driven selection to retire legacy tests and define a clean baseline

- Decision: Replace legacy spec references in suite manifests/classification and define a new canonical scenario set grouped by settings, navigation, and developer mode.
- Rationale: Existing runner scripts already consume manifest/classification artifacts, so replacing those references gives deterministic inclusion/exclusion while avoiding hidden stale specs.
- Alternatives considered:
  - Delete files only and rely on wildcard discovery: rejected because accidental inclusion/exclusion can occur.
  - Keep legacy specs marked skipped: rejected because spec requires retiring non-functional suite from active execution paths.

## Decision 3: Enforce developer-mode targeting policy with explicit not-applicable reporting

- Decision: Execute developer mode scenarios only on designated developer-mode-enabled targets; on other targets, report not-applicable without failing core suite gates.
- Rationale: This preserves required developer mode coverage while keeping CI stable for variants where developer mode is intentionally unavailable.
- Alternatives considered:
  - Require developer mode in all targets: rejected due to variant incompatibility and brittle CI.
  - Make developer mode tests optional and non-blocking everywhere: rejected because coverage would be unenforceable.

## Decision 4: Keep preflight-first gating with explicit blocker taxonomy

- Decision: Run preflight checks before e2e execution using repository scripts and classify outcomes as pass/fail/blocked/not-applicable with reproducible evidence and explicit gating rules.
- Rationale: Constitution v2.2.0 requires execution-ready environments and explicit blocked-gate handling.
- Alternatives considered:
  - Execute tests directly and infer setup failures: rejected because root-cause triage becomes ambiguous.
  - Manual environment verification by contributors: rejected as non-deterministic and not CI-safe.

## Decision 5: Keep GitHub Actions integration in existing workflows and artifact contracts

- Decision: Integrate rebuilt suite into existing CI gates, primarily [android-ci.yml](../../.github/workflows/android-ci.yml) e2e-primary path, with dual-emulator and nightly matrix workflows for extended coverage.
- Rationale: Existing workflows already bootstrap Node, Playwright, prerequisites, and artifact uploads; rebuild should optimize test definitions, not reinvent CI plumbing.
- Alternatives considered:
  - Create a brand-new workflow stack: rejected due to duplication and maintenance overhead.
  - Local-only e2e with optional CI: rejected because spec requires GitHub Actions execution as a quality gate.

## Decision 6: Preserve app architecture boundaries by limiting feature changes to test/CI surface

- Decision: Confine implementation to [testing/e2e](../../testing/e2e), preflight scripts, and workflow/test metadata updates; avoid business logic changes unless selector hooks are strictly required.
- Rationale: Feature goal is test suite rebuild and reliability, not product behavior changes.
- Alternatives considered:
  - Add app-side test-only behavior toggles globally: rejected unless a specific scenario cannot be made deterministic otherwise.

## Decision 7: Enforce rolling reliability evaluation on unchanged-code required profiles

- Decision: Compute reliability from the latest 20 unchanged-code CI runs of required PR-gate profiles and require at least 19/20 runs without nondeterministic failures.
- Rationale: This directly operationalizes SC-003 and prevents anecdotal reliability claims.
- Alternatives considered:
  - Use a shorter 5-run window: rejected as too noisy for stability conclusions.
  - Measure across all profile types including optional/nightly only: rejected because required gate quality is the merge-critical signal.

## Decision 8: Standardize triage evidence and 15-minute classification SLA

- Decision: Require a triage summary artifact for each failed CI run containing failure timestamp, scenario ID, root-cause class (product defect, environment blocker, test defect), and first maintainer classification timestamp.
- Rationale: This provides measurable evidence for SC-004 and speeds incident response.
- Alternatives considered:
  - Rely on ad-hoc issue comments: rejected because timing and structure are not enforceable.
  - Capture only logs without summary metadata: rejected because root-cause attribution becomes inconsistent.

## Decision 9: Require Playwright planner/generator/healer agents in rebuild workflow

- Decision: Use Playwright planner for scenario planning, Playwright generator for test/spec authoring, and Playwright healer for CI/local failure remediation; archive resulting evidence in test-results artifacts.
- Rationale: The clarified scope explicitly mandates agent-assisted workflow consistency and traceable outputs.
- Alternatives considered:
  - Manual-only authoring/debugging without agents: rejected because it violates FR-021.
  - Use only one Playwright agent for all phases: rejected because planning, generation, and healing have distinct responsibilities.
