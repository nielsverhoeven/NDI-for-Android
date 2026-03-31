# Data Model: Rebuild Android E2E Suite

## Entity: E2eScenario

- Purpose: Defines a single executable e2e user journey.
- Fields:
  - scenarioId (string, required, unique)
  - title (string, required)
  - featureArea (enum: settings | navigation | developer-mode, required)
  - tags (string[], required, includes execution profile tags)
  - requiredTargetCapabilities (string[], optional)
  - specPath (string, required)
  - isLegacy (boolean, required, default false)
  - enabled (boolean, required, default true)
- Validation:
  - scenarioId must be stable and unique across suite.
  - featureArea must match one supported domain.
  - specPath must resolve to an existing test spec.
  - Legacy scenarios cannot be enabled in active primary gate.

## Entity: E2eSuite

- Purpose: Groups scenarios for a selectable execution profile.
- Fields:
  - suiteId (string, required, unique)
  - name (string, required)
  - profileType (enum: pr-primary | dual-emulator | matrix-nightly, required)
  - scenarioIds (string[], required, non-empty)
  - requiresPreflight (boolean, required, default true)
  - allowsNotApplicable (boolean, required)
- Validation:
  - All scenarioIds must map to existing E2eScenario records.
  - pr-primary must include settings + navigation coverage.
  - developer-mode scenarios in non-capable targets must be reported not-applicable when allowsNotApplicable=true.

## Entity: ExecutionTarget

- Purpose: Describes runtime capabilities used for suite selection and result classification.
- Fields:
  - targetId (string, required, unique)
  - environment (enum: local | github-actions, required)
  - os (enum: windows-latest | windows-local, required)
  - emulatorCount (integer, required, min 1)
  - developerModeAvailable (boolean, required)
  - sdkReady (boolean, required)
  - ndiPrereqsReady (boolean, required)
- Validation:
  - emulatorCount must satisfy selected suite prerequisites.
  - developer-mode suite execution requires developerModeAvailable=true.

## Entity: PreflightCheckResult

- Purpose: Captures environment readiness before test execution.
- Fields:
  - runId (string, required)
  - checkName (string, required)
  - status (enum: success | failure, required)
  - details (string, optional)
  - command (string, required)
  - timestampUtc (datetime, required)
- Validation:
  - Any failure must block suite execution and emit blocker evidence.

## Entity: E2eRunResult

- Purpose: Stores outcome for scenario/suite execution and reporting.
- Fields:
  - runId (string, required, unique)
  - suiteId (string, required)
  - targetId (string, required)
  - outcome (enum: pass | fail | blocked | not-applicable, required)
  - failingScenarioIds (string[], optional)
  - artifactPaths (string[], required)
  - classification (enum: product-defect | environment-blocker | not-applicable, required)
  - summaryPath (string, required)
- Validation:
  - blocked outcome requires classification=environment-blocker and unblock guidance.
  - not-applicable outcome requires designated target rule (developer mode unavailability).
  - fail outcome requires at least one failingScenarioId.

## Relationships

- E2eSuite 1..* -> E2eScenario
- ExecutionTarget 1..* -> E2eRunResult
- E2eRunResult 1..* -> PreflightCheckResult

## State Transitions

- Scenario lifecycle:
  - Draft -> Active -> Deprecated -> Retired
- Run lifecycle:
  - Initialized -> PreflightPassed | PreflightFailed(blocked)
  - PreflightPassed -> Executing -> Pass | Fail | NotApplicable

## Notes

- This feature treats legacy tests as Retired and excludes them from Active suites.
- NotApplicable is a first-class outcome only for sanctioned target-availability conditions (developer mode policy).
