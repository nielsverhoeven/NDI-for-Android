# Phase 0 Research: Dual-Emulator NDI Output Validation with Publisher Screen Share

## Decision 1: Real Android-device automation replaces the browser placeholder

- Decision: Implement the end-to-end suite with Playwright's Android device API,
  coordinated by `testing/e2e/scripts/run-dual-emulator-e2e.ps1` and `adb`, and
  retire the current placeholder pattern in
  `testing/e2e/tests/interop-dual-emulator.spec.ts` that only opens a browser
  page and marks itself `test.fail(...)`.
- Rationale: The user-requested workflow must drive two Android app instances,
  not a simulated web surface. Playwright already exists in the repo and can be
  used as the constitution-default E2E framework while targeting Android devices.
- Alternatives considered: Keeping the browser scaffold (rejected because it
  does not validate the app), switching to Espresso-only automation (rejected by
  the Playwright-first constitution), relying on manual adb scripts only
  (rejected because they provide poor assertions and weak regression coverage).

## Decision 2: Model emulator A screen share as a reserved local source identity

- Decision: Represent the publisher's own screen as a reserved local source ID in
  the output flow, using a namespace such as `device-screen:<hostInstanceId>`
  rather than introducing a separate screen-share-only navigation stack.
- Rationale: This preserves the existing output route shape (`sourceId`), keeps
  selection semantics canonical, and lets output/viewer logic continue to reason
  in terms of source identity instead of branching on an entirely different UI.
- Alternatives considered: Adding a parallel "screen share" feature flow
  unrelated to source selection (rejected because it duplicates UX and routing),
  using display-name-only matching (rejected due to collisions and ambiguity).

## Decision 3: Screen capture uses explicit `MediaProjection` consent only

- Decision: Use Android's `MediaProjection` consent flow for publisher screen
  capture, triggered only from an explicit operator action on emulator A, with no
  new dangerous manifest permission additions.
- Rationale: This satisfies the least-permission gate while matching Android's
  platform-approved mechanism for screen sharing. It also makes privacy intent
  explicit and testable.
- Alternatives considered: Adding speculative capture permissions (rejected by
  least-permission policy), bypassing consent (rejected as unsupported and
  unsafe), limiting validation to pre-existing inbound NDI sources (rejected
  because the user explicitly requested screen sharing from emulator A).

## Decision 4: Output lifecycle includes consent readiness as a pre-start guard

- Decision: Keep the core output state machine `READY -> STARTING -> ACTIVE ->
  STOPPING -> STOPPED` with `INTERRUPTED` fault handling, and add consent as a
  prerequisite for `device-screen:*` sources rather than a separate output state.
- Rationale: This avoids exploding the public state model while still making
  publisher readiness deterministic: start cannot proceed until consent is
  granted for screen-share sources.
- Alternatives considered: Adding extra public states such as
  `CONSENT_REQUESTING`/`CONSENT_DENIED` (rejected because they are better modeled
  as UI events and error details), reducing lifecycle to a boolean flag
  (rejected due to weak observability).

## Decision 5: Start/stop remains idempotent and single-session per instance

- Decision: Preserve one active output session per app instance and treat rapid
  repeated start/stop taps as idempotent intent, even when consent or transport
  setup is in flight.
- Rationale: The publisher automation must remain stable under repeated user or
  automation actions, and duplicate NDI sender sessions would produce flaky E2E
  behavior and incorrect status.
- Alternatives considered: Queueing every action (rejected because it may replay
  stale requests), ignoring duplicates without state feedback (rejected because
  it obscures operator intent).

## Decision 6: Persist only continuity data needed to resume operator intent

- Decision: Continue storing operator defaults and continuity metadata in Room:
  preferred stream name, last selected output source ID, and last known output
  summary. Store validation artifacts as files under `testing/e2e` outputs rather
  than in-app persistence.
- Rationale: This satisfies offline-first continuity without polluting app data
  with transient host-side automation evidence.
- Alternatives considered: In-memory-only continuity (rejected for poor restart
  resilience), storing automation evidence in Room (rejected because it couples
  app state with external test harness concerns).

## Decision 7: Dual-emulator run topology is mandatory app-to-app interoperability

- Decision: Require one canonical automated scenario where emulator A publishes
  its screen via this app, emulator B discovers that outbound stream using this
  app's source list, opens it in the viewer, and then observes the stop/
  interruption behavior when emulator A stops publishing.
- Rationale: This is the precise regression path requested by the user and the
  highest-value proof that both implemented features interoperate in practice.
- Alternatives considered: Third-party receiver validation only (rejected
  because it does not confirm app-to-app compatibility), single-emulator loopback
  (rejected because it cannot prove cross-instance discovery and playback).

## Decision 8: Preflight must validate emulator identity, installation, and network

- Decision: Make preflight checks mandatory for emulator serial uniqueness,
  device reachability, app installation state, and same-segment multicast-capable
  networking before the Playwright test begins assertions.
- Rationale: NDI discovery failures are often topology-related; explicit preflight
  eliminates avoidable false negatives and makes failure reports actionable.
- Alternatives considered: Allowing the test to fail deep in playback assertions
  without preflight (rejected because it masks environment failures), requiring
  physical devices only (rejected because the user requested emulators).

## Decision 9: Validation evidence must include artifacts from both roles

- Decision: Capture per-run evidence including publisher/receiver serials,
  timestamps, discovery and playback latencies, screenshots/video on failure,
  and logcat exports for both emulators.
- Rationale: Dual-device failures are otherwise difficult to diagnose, and the
  release gate requires reproducible evidence, not just a pass/fail bit.
- Alternatives considered: Retaining only Playwright console output (rejected
  because it is insufficient for transport/debug analysis), storing no metrics
  (rejected because success criteria cannot be aggregated reliably).

## Decision 10: Test strategy stays Red-Green-Refactor with Playwright at the top

- Decision: Preserve strict TDD with three layers: JUnit tests for ViewModel and
  repository logic, Android compatibility/instrumentation tests for platform
  regressions, and Playwright Android dual-emulator tests for publish ->
  discover -> play -> stop interoperability.
- Rationale: Logic coverage alone cannot prove network interoperability, while
  E2E-only coverage would be too slow and brittle for all behavior changes.
- Alternatives considered: Manual validation only (rejected for regression risk),
  unit tests only (rejected because cross-device behavior is core), Espresso as
  the primary E2E driver (rejected because Playwright is constitution-default).

## Decision 11: Toolchain baseline is taken from the active branch, not stale docs

- Decision: Use the branch's actual checked-in configuration as authoritative for
  planning: Gradle wrapper 9.2.1, AGP 9.0.0, Kotlin plugin 2.2.10, compileSdk /
  targetSdk 34, Java toolchain 21 with Java 17 bytecode targets, while keeping
  `TOOLCHAIN-001` open until validation artifacts are synchronized.
- Rationale: The repository's build files and verified wrapper output are more
  trustworthy than older guidance text when they disagree.
- Alternatives considered: Planning against older documented versions (rejected
  because it would make the plan immediately inconsistent with the codebase),
  closing `TOOLCHAIN-001` early (rejected because dual-emulator and release
  validation evidence are still incomplete).
