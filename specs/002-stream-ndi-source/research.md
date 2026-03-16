# Phase 0 Research: NDI Source Network Output with Dual-Emulator End-to-End Validation

## Decision 1: Outbound NDI publishing architecture

- Decision: Implement outbound NDI streaming through a dedicated output repository
  in `feature/ndi-browser/domain` and `feature/ndi-browser/data`, with native
  send/start/stop calls isolated in `ndi/sdk-bridge`.
- Rationale: Keeps MVVM/repository boundaries intact and avoids direct JNI/native
  dependencies in UI or ViewModels.
- Alternatives considered: Driving native sender directly from ViewModel
  (rejected for architecture/testability violations), placing sender logic in
  `app` module (rejected due to feature-boundary leakage).

## Decision 2: Input-to-output source model

- Decision: Use existing source identity semantics (`sourceId` as canonical key)
  and map selected input source to one outbound output session.
- Rationale: Prevents ambiguity across duplicate display names and aligns with
  prior feature contracts.
- Alternatives considered: Display-name-only linkage (rejected for collisions),
  index-based linkage (rejected for instability across refreshes).

## Decision 3: Output lifecycle state machine

- Decision: Standardize output states as READY -> STARTING -> ACTIVE -> STOPPING
  -> STOPPED, with INTERRUPTED as fault state and bounded retry entry.
- Rationale: Explicit state machine supports reliable UX, deterministic tests,
  and prevention of duplicate start/stop actions.
- Alternatives considered: Boolean active/inactive only (rejected due to missing
  transitional/error observability), unbounded retry loops (rejected for battery
  and operational predictability concerns).

## Decision 4: Idempotent start/stop behavior

- Decision: Treat rapid repeated start/stop taps as idempotent intent; only one
  active output session may exist at a time for an app instance.
- Rationale: Prevents duplicate sender sessions and unstable UI status.
- Alternatives considered: Queueing all actions (rejected because it can produce
  outdated or unsafe state transitions), ignoring extra taps silently (rejected
  due to poor user feedback).

## Decision 5: Persistence strategy

- Decision: Persist operator defaults (outbound stream name preference, last
  selected source identity, last known output state summary) in Room.
- Rationale: Satisfies offline-first policy while keeping continuity state
  recoverable after process death or restart.
- Alternatives considered: Volatile memory only (rejected for poor resilience),
  SharedPreferences-only storage (rejected for inconsistency with Room-first
  offline reliability policy).

## Decision 6: Telemetry scope

- Decision: Emit non-sensitive telemetry for output_start_requested,
  output_started, output_stopped, output_interrupted, output_retry_requested,
  output_retry_succeeded, output_retry_failed.
- Rationale: Supports reliability diagnostics without capturing sensitive payload.
- Alternatives considered: No telemetry (rejected due to reduced observability),
  full payload capture (rejected for privacy/security constraints).

## Decision 7: Dual-emulator E2E topology

- Decision: Make a mandatory cross-feature E2E scenario where emulator A runs
  this app in publisher role and streams its screen as an NDI source, while
  emulator B runs this app in receiver role using the previously implemented
  discovery/viewer flow to capture and render emulator A's stream.
- Rationale: This directly validates interoperability between the new output
  feature and the previous capture feature in the exact workflow requested.
- Alternatives considered: Testing publisher with a third-party receiver only
  (rejected because it does not verify app-to-app interoperability), single
  emulator loopback validation (rejected because it does not represent
  independent sender/receiver instances).

## Decision 8: Emulator network assumptions and safeguards

- Decision: Define the E2E test precondition as both emulators being on the same
  multicast-capable network segment, with explicit preflight checks before
  running publish->discover->play assertions.
- Rationale: NDI discovery/transport behavior depends on network topology and
  multicast reachability; preflight avoids false negatives.
- Alternatives considered: Ignoring network preflight (rejected due to flaky
  validation), requiring only physical devices (rejected because emulator-based
  validation is a stated requirement).

## Decision 9: Test strategy under constitution TDD gate

- Decision: Use Red-Green-Refactor with layered coverage: unit tests for output
  state transitions/retry, repository contract tests for sender lifecycle, and
  instrumentation tests for dual-emulator publish->discover->play->stop flow.
- Rationale: Provides deterministic coverage of logic and validates real app-to-
  app behavior.
- Alternatives considered: Manual test-only approach (rejected for regression
  risk), unit tests only (rejected because networked app-to-app behavior is core).

## Decision 10: Toolchain governance continuity

- Decision: Reuse existing blocker `TOOLCHAIN-001` for baseline lag tracking and
  require release-mode validation (R8/ProGuard + E2E flow) after any toolchain-
  affecting change.
- Rationale: Maintains constitutional compliance and avoids fragmented tracking.
- Alternatives considered: Creating a second blocker for the same baseline lag
  (rejected as duplicate governance overhead), skipping blocker references in
  this feature (rejected for policy noncompliance).
