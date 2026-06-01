# Data Model: Dual-Emulator NDI Latency Measurement

## Entity: LatencyMeasurementRun

- Purpose: Represents one complete latency scenario execution.
- Fields:
  - runId (string, required): unique identifier.
  - triggerType (enum, required): LOCAL | PR | SCHEDULED.
  - publisherSerial (string, required).
  - receiverSerial (string, required).
  - startedAtEpochMillis (long, required).
  - completedAtEpochMillis (long, optional until completion).
  - status (enum, required): PASSED | FAILED | INCOMPLETE.
  - failureReason (string, optional).
- Validation rules:
  - PASSED requires completedAtEpochMillis and latency result reference.
  - FAILED/INCOMPLETE require explicit failureReason.

## Entity: RecordingArtifact

- Purpose: Captured video artifact for each emulator role.
- Fields:
  - artifactId (string, required).
  - runId (string, required, references LatencyMeasurementRun).
  - role (enum, required): SOURCE | RECEIVER.
  - filePath (string, required).
  - durationMillis (long, required).
  - resolution (string, optional).
  - isUsableForAnalysis (boolean, required).
- Validation rules:
  - Both SOURCE and RECEIVER artifacts required for valid analysis.

## Entity: LatencyAnalysisResult

- Purpose: Structured output of latency computation.
- Fields:
  - analysisId (string, required).
  - runId (string, required, references LatencyMeasurementRun).
  - method (enum, required): MOTION_CROSS_CORRELATION.
  - estimatedLatencyMs (number, optional when invalid).
  - confidenceScore (number, optional).
  - validity (enum, required): VALID | INVALID.
  - invalidReason (string, optional).
  - outputPath (string, required).
- Validation rules:
  - VALID requires estimatedLatencyMs.
  - INVALID requires invalidReason.

## Entity: ScenarioCheckpoint (formerly ScenarioStepCheckpoint)

- Purpose: Step-level execution trace.
- Fields:
  - checkpointId (string, required): unique identifier.
  - runId (string, required).
  - stepName (enum, required): START_STREAM_A | START_VIEW_B | START_RECORDING | START_YOUTUBE | VERIFY_VIEWER_PLAYBACK | YOUTUBE_UNAVAILABLE | ANALYZE_LATENCY.
  - status (enum, required): PASS | FAIL | SKIP.
  - timestampEpochMillis (long, required).
  - detail (string, optional).
- Validation rules:
  - At most one checkpoint may be FAIL for terminal run failure reason.
  - YOUTUBE_UNAVAILABLE step terminates scenario and marks run invalid with explicit unavailability reason.

## Entity: QualityGateEvidence

- Purpose: Human-review summary for CI and release readiness.
- Fields:
  - evidenceId (string, required).
  - runId (string, required).
  - summaryPath (string, required).
  - includesLatencyResult (boolean, required).
  - includesRecordings (boolean, required).
  - includesRegressionOutcome (boolean, required).
- Validation rules:
  - Merge/release-ready evidence requires all include flags set true.

## Relationships

- LatencyMeasurementRun has two required RecordingArtifact entries (SOURCE and RECEIVER).
- LatencyMeasurementRun has one LatencyAnalysisResult.
- LatencyMeasurementRun has ordered ScenarioCheckpoint entries.
- QualityGateEvidence references LatencyMeasurementRun and aggregates outputs.

## State Transitions

- LatencyMeasurementRun:
  - CREATED -> RUNNING -> PASSED
  - CREATED -> RUNNING -> FAILED
  - CREATED -> RUNNING -> INCOMPLETE
- LatencyAnalysisResult:
  - PENDING -> VALID
  - PENDING -> INVALID
