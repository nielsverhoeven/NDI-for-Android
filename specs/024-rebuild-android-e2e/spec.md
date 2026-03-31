# Feature Specification: Rebuild Android E2E Suite

**Feature Branch**: `[024-rebuild-android-e2e]`  
**Created**: 2026-03-31  
**Status**: Draft  
**Input**: User description: "I want to rebuild the entire e2e test suite because it is not working properly. Remove all existing e2e tests. Build new e2e tests wherever possible, at least for the settings menu and the navigation menu and the developer mode. Make sure the e2e tests are runnable from GitHub Actions."

## Clarifications

### Session 2026-03-31

- Q: How should developer mode e2e tests behave across build variants where developer mode may not be available? → A: Run developer mode e2e tests on designated developer-mode-enabled CI targets; on other targets report not-applicable without failing the core suite.
- Q: Which execution status taxonomy should CI and reports use? → A: Use pass/fail/blocked/not-applicable as the canonical set; required profiles gate on fail and blocked, while not-applicable is informational for intentionally unsupported targets.
- Q: How should reliability be measured for the 95% stability target? → A: Measure pass-rate across the latest 20 unchanged-code runs of required PR-gate profiles on default-branch CI history; success requires at least 19 of 20 runs without nondeterministic failure.
- Q: What evidence proves the 15-minute triage objective? → A: Each failed CI run must publish a triage summary artifact with failure timestamp, scenario ID, root-cause category, and first maintainer classification timestamp not exceeding 15 minutes.
- Q: How should currently open edge-case questions be resolved? → A: Convert each to deterministic behavior rules covering install-fail classification, slow-runner timing controls, feature-flag-hidden menu handling, and missing setup-data blocked outcomes.

## User Scenarios & Testing *(mandatory)*

<!--
  IMPORTANT: User stories should be PRIORITIZED as user journeys ordered by importance.
  Each user story/journey must be INDEPENDENTLY TESTABLE - meaning if you implement just ONE of them,
  you should still have a viable MVP (Minimum Viable Product) that delivers value.
  
  Assign priorities (P1, P2, P3, etc.) to each story, where P1 is the most critical.
  Think of each story as a standalone slice of functionality that can be:
  - Developed independently
  - Tested independently
  - Deployed independently
  - Demonstrated to users independently
-->

### User Story 1 - Rebuild Core E2E Coverage (Priority: P1)

As a maintainer, I need a fully replaced and stable Android e2e test suite so core user journeys are reliably validated and regressions are caught before release.

**Why this priority**: The existing suite is not trusted; replacing it is the critical path for release confidence.

**Independent Test**: Can be fully tested by executing the new suite in a local Android test environment and confirming deterministic pass/fail outcomes for core flows.

**Acceptance Scenarios**:

1. **Given** legacy e2e tests exist, **When** the rebuild is completed, **Then** legacy e2e tests are removed from the active suite and only the new suite is used for e2e validation.
2. **Given** the app is installed in test mode, **When** the new e2e suite is executed, **Then** it validates core navigation and settings workflows without manual intervention.

---

### User Story 2 - Validate Settings and Navigation Menus (Priority: P2)

As a maintainer, I need dedicated e2e tests for the settings menu and navigation menu so that screen access and menu interactions are protected from regressions.

**Why this priority**: These are high-frequency user entry points and must remain stable across changes.

**Independent Test**: Can be tested independently by running only menu-focused e2e scenarios and verifying expected screen transitions and visible state changes.

**Acceptance Scenarios**:

1. **Given** a fresh app launch, **When** a user opens and interacts with the navigation menu, **Then** the expected destinations are reachable and clearly indicated.
2. **Given** a user opens the settings menu, **When** they interact with available settings controls, **Then** expected UI responses and persisted setting states are observed.

---

### User Story 3 - Validate Developer Mode Flows (Priority: P3)

As a maintainer, I need developer mode e2e coverage so advanced/debug-only controls remain functional and safe to change.

**Why this priority**: Developer mode is less frequent than primary navigation but has high operational impact when broken.

**Independent Test**: Can be tested independently by running only developer mode scenarios on designated developer-mode-enabled targets and verifying entry, toggle behavior, and safe exit behavior.

**Acceptance Scenarios**:

1. **Given** developer mode is available, **When** it is enabled and configured through intended UI flows, **Then** developer mode indicators and options reflect the selected state.
2. **Given** developer mode is enabled, **When** it is disabled, **Then** the app returns to normal-mode behavior with developer-only options hidden or inactive.

---

### User Story 4 - Run E2E in GitHub Actions (Priority: P1)

As a repository maintainer, I need the rebuilt e2e suite to run in GitHub Actions so pull requests receive automated end-to-end validation.

**Why this priority**: CI execution is required to make the new suite enforceable and valuable for team workflows.

**Independent Test**: Can be tested independently by running the workflow in GitHub Actions and confirming e2e steps execute and publish canonical status outcomes and gating signals.

**Acceptance Scenarios**:

1. **Given** a pull request triggers CI, **When** the e2e workflow starts, **Then** required preflight checks run before e2e execution.
2. **Given** e2e tests run in CI, **When** tests complete, **Then** the workflow reports canonical outcomes (pass/fail/blocked/not-applicable) with required gating signals and includes logs/artifacts needed for triage.

### Visual Change Quality Gate *(mandatory when UI changes are present)*

- This feature changes visual validation behavior by replacing the e2e suite.
- The new e2e suite MUST run on Android emulator(s) and cover user-visible flows for settings menu, navigation menu, and developer mode.
- Regression coverage MUST include execution of all newly defined e2e scenarios on every CI run that includes e2e validation.

### Test Environment & Preconditions *(mandatory)*

- Required runtime dependencies:
  - Android SDK and command line tools compatible with the repository baseline.
  - At least one booted emulator for local e2e execution and CI-compatible emulator configuration for GitHub Actions.
  - NDI prerequisites required by the project validation scripts.
- Required preflight checks before e2e execution:
  - `scripts/verify-android-prereqs.ps1`
  - `scripts/verify-e2e-dual-emulator-prereqs.ps1` when scenarios require dual-device behavior.
- If environment dependencies are unavailable (for example, no emulator/device or missing SDK components), validation results MUST be marked as blocked with:
  - exact failed preflight check,
  - timestamped execution context,
  - concrete remediation step to unblock.

### Edge Cases

- When developer mode is not accessible in a build variant, developer mode scenarios are reported as not-applicable and do not fail the core suite for that target.
- If emulator boot succeeds but app install fails, the run result MUST be classified as blocked and include install command output plus explicit reinstall/uninstall remediation steps.
- Timing-sensitive assertions MUST use bounded explicit waits and retry-once assertion wrappers with fixed upper limits so slower CI runners do not introduce nondeterministic outcomes.
- If a required menu item is intentionally hidden by feature flags or first-run gating, the scenario MUST record not-applicable with the gating condition artifact; if the item is expected but missing, the scenario MUST fail.
- If preflight passes but scenario setup data is unavailable, the run MUST be classified as blocked with a setup-data-missing reason code and regeneration command.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The project MUST retire the current non-functional e2e suite from active execution paths.
- **FR-002**: The project MUST provide a newly authored Android e2e suite that replaces retired tests.
- **FR-003**: The new suite MUST include end-to-end scenarios for settings menu behavior.
- **FR-004**: The new suite MUST include end-to-end scenarios for navigation menu behavior.
- **FR-005**: The new suite MUST include end-to-end scenarios for developer mode behavior.
- **FR-006**: Each required scenario MUST define deterministic pass/fail assertions based on user-visible outcomes.
- **FR-007**: The suite MUST be executable through a documented command path suitable for local validation and CI automation.
- **FR-008**: GitHub Actions MUST execute the e2e suite as part of repository automation for pull request or protected-branch validation.
- **FR-009**: CI e2e execution MUST run preflight checks before test execution and fail fast on unmet prerequisites.
- **FR-010**: CI runs MUST publish logs and artifacts sufficient to diagnose failed or blocked e2e outcomes.
- **FR-011**: Validation reports MUST classify outcomes using pass, fail, blocked, or not-applicable and include clear blocker/gating reasons.
- **FR-012**: Test definitions MUST be organized so menu coverage and developer mode coverage can be executed independently.
- **FR-013**: The rebuilt suite MUST avoid dependency on deprecated or removed test cases from the retired suite.
- **FR-014**: Developer mode e2e scenarios MUST execute on designated developer-mode-enabled CI targets and MUST be reported as not-applicable (not failed) on targets where developer mode is intentionally unavailable.
- **FR-015**: CI reporting MUST distinguish not-applicable developer mode results from pass/fail outcomes while preserving required pass/fail gating for settings and navigation scenarios.
- **FR-016**: During legacy-to-rebuilt suite handover, the project MUST record transition evidence that captures a pre-rebuild Playwright baseline run and a rebuilt-suite comparison report proving required gate coverage continuity.
- **FR-017**: CI reliability measurement MUST be computed on the latest 20 unchanged-code runs of required PR-gate profiles, with a minimum 95% nondeterministic-failure-free pass rate.
- **FR-018**: For each failed CI run, a triage summary artifact MUST include failure timestamp, scenario ID, root-cause category (product defect, environment blocker, or test defect), and first classification timestamp.
- **FR-019**: Required profile gating MUST fail on fail or blocked outcomes and MUST NOT fail on not-applicable outcomes from intentionally unsupported targets.
- **FR-020**: Local and CI execution documentation MUST include at least one validated command contract path that is executable end-to-end without undocumented manual steps.
- **FR-021**: This feature's e2e rebuild workflow MUST use Playwright planner, generator, and healer agents for scenario planning, scenario authoring, and failure remediation, with produced evidence captured in test results.

### Non-Functional Requirements

- **NFR-001 (Reliability)**: Over a rolling window of 20 unchanged-code required-profile CI runs, at least 19 runs MUST complete without nondeterministic failures.
- **NFR-002 (Observability)**: Every CI e2e run MUST publish machine-readable status and triage artifacts that include status taxonomy value, scenario identifiers, and blocker or root-cause metadata.
- **NFR-003 (Operability)**: For failed runs, first maintainer classification time MUST be within 15 minutes from failure timestamp as evidenced in triage artifacts.
- **NFR-004 (Determinism)**: Test timing controls MUST use bounded waits and deterministic retry policy to limit CI vs local timing drift impact.
- **NFR-005 (Tooling Consistency)**: Playwright remains the default e2e framework and Playwright agents are required for planning, generation, and healing activities in this feature scope.

### Key Entities *(include if feature involves data)*

- **E2E Test Scenario**: Defines one end-to-end user journey, including preconditions, steps, and expected outcomes.
- **E2E Test Suite**: Collection of scenarios grouped by feature area (settings, navigation, developer mode) with independent execution support.
- **Validation Run Result**: Record of one local or CI execution including status (pass/fail/blocked), preflight outcomes, and diagnostic artifacts.
- **CI E2E Job**: Automated execution unit in GitHub Actions responsible for environment setup, preflight checks, test run, and artifact publication.

## Assumptions

- Existing e2e tests can be removed without preserving backward compatibility for old test identifiers.
- Developer mode is available in at least one supported test target used by CI.
- At least one GitHub-hosted or self-hosted runner configuration can boot required Android emulator(s) within acceptable time limits.
- Test data and app startup conditions required for settings/navigation/developer mode scenarios can be controlled in CI.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of legacy e2e tests are retired from active e2e execution within this feature scope.
- **SC-002**: New automated e2e coverage exists for settings menu, navigation menu, and developer mode, with at least one passing scenario per area.
- **SC-003**: In CI, at least 95% (minimum 19/20) of unchanged-code runs for required PR-gate profiles complete without nondeterministic failures over a rolling 20-run window.
- **SC-004**: For any failed CI e2e run, maintainers can identify the failing scenario and root-cause category (product defect, environment blocker, or test defect) within 15 minutes using published triage artifacts.
- **SC-005**: Pull requests that trigger e2e validation receive a clear automated status using pass/fail/blocked/not-applicable taxonomy before merge decision, with gating determined by required profile rules.
