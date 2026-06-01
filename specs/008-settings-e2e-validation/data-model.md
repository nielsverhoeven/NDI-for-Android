# Data Model: Settings Menu End-to-End Emulator Validation

## Entity: E2EScenario

- Purpose: Defines one executable end-to-end scenario.
- Fields:
  - scenarioId (string, required): stable identifier (for example `settings-source-open-back`).
  - story (enum, required): US1 | US2 | US3.
  - category (enum, required): NEW_SETTINGS | EXISTING_REGRESSION.
  - description (string, required).
  - emulatorProfileTag (string, required): primary or matrix profile key.
  - tags (list<string>, optional): smoke, regression, timing, fallback.
- Validation rules:
  - scenarioId must be unique within a suite.
  - category NEW_SETTINGS requires mapping to one settings acceptance scenario.

## Entity: EmulatorProfile

- Purpose: Captures emulator runtime profile used by validation.
- Fields:
  - profileId (string, required): unique CI key.
  - apiLevel (int, required).
  - abi (string, required).
  - isPrimaryPrProfile (boolean, required).
  - isScheduledMatrixProfile (boolean, required).
- Validation rules:
  - Exactly one profile must have isPrimaryPrProfile=true.
  - At least one profile must be marked for scheduled matrix runs.

## Entity: ValidationRun

- Purpose: Represents one full execution cycle used for quality-gate review.
- Fields:
  - runId (string, required).
  - triggerType (enum, required): PR | SCHEDULED.
  - startedAtEpochMillis (long, required).
  - completedAtEpochMillis (long, optional until completion).
  - status (enum, required): PASSED | FAILED | INCOMPLETE.
  - profilesExecuted (list<EmulatorProfile>, required).
- Validation rules:
  - PR runs must execute the primary profile.
  - INCOMPLETE runs fail acceptance and require rerun.

## Entity: SuiteResult

- Purpose: Records outcome for a scenario grouping in a validation run.
- Fields:
  - suiteType (enum, required): NEW_SETTINGS_SUITE | EXISTING_REGRESSION_SUITE.
  - passedCount (int, required).
  - failedCount (int, required).
  - skippedCount (int, required).
  - durationMillis (long, required).
- Validation rules:
  - For accepted PR runs, failedCount must be 0 for both suite types.
  - skippedCount must be 0 for required scenarios unless a documented waiver exists.

## Entity: QualityGateEvidence

- Purpose: Review artifact proving compliance with required checks.
- Fields:
  - evidenceId (string, required).
  - runId (string, required, references ValidationRun).
  - artifactPath (string, required): report/log file location.
  - includesNewSettingsOutcome (boolean, required).
  - includesExistingRegressionOutcome (boolean, required).
  - reviewerVisibleSummary (string, required).
- Validation rules:
  - Both include flags must be true for merge-ready evidence.

## Relationships

- ValidationRun includes one or more EmulatorProfiles.
- ValidationRun contains two required SuiteResults (new settings + existing regression).
- QualityGateEvidence references ValidationRun and summarizes suite results.
- E2EScenario instances execute under one EmulatorProfile in a run.

## State Transitions

- ValidationRun:
  - CREATED -> RUNNING -> PASSED
  - CREATED -> RUNNING -> FAILED
  - CREATED -> RUNNING -> INCOMPLETE (requires rerun)
- Quality gate decision:
  - BLOCKED when run status is FAILED or INCOMPLETE
  - APPROVED when run status is PASSED and both suite outcomes are present
