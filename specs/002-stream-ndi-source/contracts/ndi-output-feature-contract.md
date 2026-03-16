# Contract: NDI Output Feature and Cross-Feature Interoperability

## 1. Repository Contracts

### 1.1 NdiOutputRepository

- startOutput(inputSourceId: string, streamName: string): OutputSession
- stopOutput(): OutputSession
- observeOutputSession(): stream of OutputSession
- retryInterruptedOutputWithinWindow(windowSeconds = 15): OutputSession
- observeOutputHealth(): stream of OutputHealthSnapshot

Behavioral requirements:

- At most one active output session exists per app instance.
- startOutput is idempotent for repeated rapid user actions while STARTING/ACTIVE.
- stopOutput transitions ACTIVE/INTERRUPTED/STARTING to STOPPING then STOPPED.
- Retry window is bounded to 15 seconds for this feature version.

### 1.2 OutputConfigurationRepository

- savePreferredStreamName(value: string): void
- getPreferredStreamName(): string
- saveLastSelectedInputSource(sourceId: string): void
- getLastSelectedInputSource(): string?

Behavioral requirements:

- Configuration must persist through app restarts.
- Invalid stream-name values must be rejected with explicit validation outcome.

### 1.3 CrossFeatureInteropValidationRepository

- startDualEmulatorRun(publisherDeviceId: string, receiverDeviceId: string): runId
- recordPublisherOutputActive(runId: string): void
- recordReceiverDiscovered(runId: string): void
- recordReceiverPlaying(runId: string): void
- completeRun(runId: string, result: PASS | FAIL, failureReason?: string): void

Behavioral requirements:

- Validation run must fail if publisher and receiver device IDs are identical.
- PASS requires publisher active and receiver playing milestones.

## 2. ViewModel Contracts

### 2.1 OutputControlViewModel

Inputs:

- onOutputScreenVisible()
- onStreamNameChanged(value)
- onStartOutputPressed(inputSourceId)
- onStopOutputPressed()
- onRetryOutputPressed()

Outputs (state):

- outputState: READY | STARTING | ACTIVE | STOPPING | STOPPED | INTERRUPTED
- activeInputSourceId: string?
- outboundStreamName: string
- statusMessage: string?
- canStart: Boolean
- canStop: Boolean
- errorState: string?

Guarantees:

- Start and stop actions are guarded against duplicate concurrent requests.
- Interrupted state exposes retry and stop actions.

### 2.2 SourceListViewModel (interoperability extension)

Inputs:

- onSourceSelected(sourceId)
- onOpenOutputForSelectedSource()

Outputs (state/events):

- selectedSourceId: string?
- navigationEvent: OpenOutputControl(sourceId)

Guarantees:

- Selected source identity remains canonical sourceId across viewer and output flows.

### 2.3 ViewerViewModel (cross-feature verification)

Inputs:

- onViewerOpened(sourceId)

Outputs:

- playbackState: CONNECTING | PLAYING | INTERRUPTED | STOPPED

Guarantees:

- Viewer flow remains compatible with outbound stream identities produced by
  OutputControlViewModel via NdiOutputRepository.

## 3. Navigation Contract

Routes:

- SourceListRoute
- ViewerRoute(sourceId: string)
- OutputControlRoute(sourceId: string)

Rules:

- sourceId is mandatory for ViewerRoute and OutputControlRoute.
- Navigation remains single-activity and graph-based.
- Returning from output control to source list preserves selected source context.

## 4. Permission Contract

- No dangerous permission may be added without explicit spec amendment.
- Location permission remains prohibited for discovery/output flows.

## 5. Observability Contract

Required non-sensitive event categories:

- output_start_requested
- output_started
- output_stopped
- output_interrupted
- output_retry_requested
- output_retry_succeeded
- output_retry_failed
- dual_emulator_e2e_started
- dual_emulator_e2e_passed
- dual_emulator_e2e_failed

Event payload constraints:

- No raw video/audio payload data.
- No personally identifiable information.
- Device role metadata may be included only as anonymized role labels
  (publisher/receiver), not user identifiers.

## 6. Dual-Emulator End-to-End Validation Contract

Scenario requirements:

- Emulator A runs this app in publisher role and exposes an outbound NDI stream
  from its active screen/source.
- Emulator B runs this app in receiver role and uses source discovery + viewer
  flow to locate and render emulator A output.
- Both emulators must be on the same multicast-capable network segment.

Pass criteria:

- Publisher transitions to ACTIVE output state.
- Receiver discovers publisher stream within validation timeout.
- Receiver reaches PLAYING state for publisher stream.
- Stopping output on publisher transitions receiver away from active playback
  with recoverable UX.

Failure criteria:

- Receiver never discovers publisher stream within timeout.
- Receiver cannot transition to PLAYING for discovered stream.
- Publisher stop does not propagate observable playback end/interruption state.

## 7. Release Validation Contract

- Any toolchain-affecting change (compileSdk/targetSdk, AGP, Gradle, Kotlin,
  Java/JBR, AndroidX, NDK/CMake, NDI SDK) requires rerunning prerequisite checks,
  unit tests, instrumentation tests, and release build validation.
- Release validation must include at least one full dual-emulator interop run
  for publish -> discover -> play -> stop regression coverage.
- Toolchain blocker `TOOLCHAIN-001` (or successor) must remain documented with
  owner, affected components, and target resolution timeline until resolved.
