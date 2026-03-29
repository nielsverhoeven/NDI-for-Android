# Phase 0 Research - Viewer Persistence and Stream Availability Status

## Decision 1: Persist last viewed context using existing Room plus one file-backed preview image

- Decision: Reuse existing Room persistence patterns for metadata (last viewed source, timestamps, connection flags, availability counters) and store exactly one preview frame image in app-internal storage.
- Rationale: The codebase already centralizes persistence through Room in `core/database` and repository interfaces in `feature/ndi-browser/domain`; this aligns with repository-mediated data access and offline-first constraints.
- Alternatives considered:
  - SharedPreferences-only metadata plus Base64 image string: rejected due to poor scalability and less structured migration support.
  - Room BLOB for image bytes: rejected to avoid large DB payload churn and vacuum overhead for frequently updated frames.

## Decision 2: Mark Previously Connected only after first successful rendered frame

- Decision: Transition a stream to Previously Connected only when at least one video frame is available from `NdiViewerRepository.getLatestVideoFrame()` after connect starts.
- Rationale: This avoids false positives when connection attempts fail before playback starts.
- Alternatives considered:
  - Mark on connect attempt: rejected because it over-reports successful history.
  - Mark on CONNECTING state: rejected for same reason.

## Decision 3: Availability transition policy uses two consecutive missed discovery polls

- Decision: Keep stream row available until two consecutive discovery misses, then mark unavailable and disable View Stream.
- Rationale: This debounces transient network jitter while still surfacing real disconnects quickly.
- Alternatives considered:
  - One missed poll threshold: rejected as too sensitive/flappy.
  - Time-window-only threshold: rejected due to reduced determinism in tests.

## Decision 4: Keep exactly one persisted preview image (last viewed stream only)

- Decision: Replace the previous preview image whenever a different stream becomes last-viewed with at least one rendered frame.
- Rationale: Bounded storage footprint and simpler state restoration semantics.
- Alternatives considered:
  - Per-stream preview cache: rejected for scope expansion and higher storage growth.
  - No image persistence: rejected because feature explicitly requires last frame retention.

## Decision 5: Implement through existing module boundaries and service-locator dependencies

- Decision: Keep contracts in `feature/ndi-browser/domain`, data implementation in `feature/ndi-browser/data`, and wiring in `app` (`AppGraph`, `SourceListDependencies`, `ViewerDependencies`).
- Rationale: Matches project architecture rules and avoids cross-module leakage.
- Alternatives considered:
  - Direct presentation-to-database calls: rejected by constitution and existing architecture.
  - New singleton outside AppGraph: rejected due to testability and ownership concerns.

## Decision 6: Testing strategy uses JUnit for unit tests and Playwright for visual/e2e coverage

- Decision: Add failing JUnit tests first for repository/viewmodel behavior, plus emulator-run Playwright scenarios for UI indicators and restore flows, then run full Playwright regression suite.
- Rationale: Required by constitution principles IV and XII and feature visual-change gate.
- Alternatives considered:
  - Espresso-only UI tests: rejected because Playwright is the default and required for visual behavior changes.
  - Manual QA only: rejected as non-repeatable and non-compliant.

## Decision 7: Blocked-environment evidence and deterministic preflight are first-class gates

- Decision: Require preflight via `scripts/verify-android-prereqs.ps1` and capture blocked status with reproducible evidence before e2e execution.
- Rationale: Constitution principle XII mandates environment-ready validation and explicit blocked-gate classification.
- Alternatives considered:
  - Skip preflight and run tests directly: rejected due to high false-failure risk.
  - Soft preflight warnings only: rejected; gates require explicit pass/blocked outcome.
