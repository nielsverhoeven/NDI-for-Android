# Contract: NDI Output Feature, Screen Share, and Dual-Emulator Interoperability

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
- `inputSourceId` accepts discovered NDI source IDs and the reserved local
  namespace `device-screen:<hostInstanceId>`.
- `device-screen:*` sources MUST fail with an explicit consent-related outcome if
  screen-capture consent has not been granted for the current app session.
- stopOutput transitions ACTIVE/INTERRUPTED/STARTING to STOPPING then STOPPED.
- Retry window is bounded to 15 seconds for this feature version.

### 1.2 ScreenCaptureConsentRepository

- beginConsentRequest(inputSourceId: string): ScreenCaptureConsentRequest
- registerConsentResult(granted: boolean, tokenRef?: string): ScreenCaptureConsentState
- clearConsent(): void

Behavioral requirements:

- Consent may only be requested from explicit operator action.
- No dangerous manifest permission may be added as a substitute for consent.
- Denied consent must leave the feature in a recoverable READY/blocked state.

### 1.3 OutputConfigurationRepository

- savePreferredStreamName(value: string): void
- getPreferredStreamName(): string
- saveLastSelectedInputSource(sourceId: string): void
- getLastSelectedInputSource(): string?

Behavioral requirements:

- Configuration must persist through app restarts.
- Invalid stream-name values must be rejected with explicit validation outcome.
- Reserved `device-screen:*` source IDs may be persisted as the last selected
  output source.

### 1.4 CrossFeatureInteropValidationRepository

- startDualEmulatorRun(publisherDeviceId: string, receiverDeviceId: string): runId
- recordPublisherConsentStarted(runId: string): void
- recordPublisherConsentGranted(runId: string): void
- recordPublisherOutputActive(runId: string): void
- recordReceiverDiscovered(runId: string): void
- recordReceiverPlaying(runId: string): void
- completeRun(runId: string, result: PASS | FAIL, failureReason?: string): void

Behavioral requirements:

- Validation run must fail if publisher and receiver device IDs are identical.
- Validation run must fail if network/install preflight has not passed.
- PASS requires publisher active and receiver playing milestones.

## 2. ViewModel Contracts

### 2.1 OutputControlViewModel

Inputs:

- onOutputScreenVisible()
- onStreamNameChanged(value)
- onStartOutputPressed(inputSourceId)
- onScreenCaptureConsentResult(granted, tokenRef?)
- onStopOutputPressed()
- onRetryOutputPressed()

Outputs (state):

- outputState: READY | STARTING | ACTIVE | STOPPING | STOPPED | INTERRUPTED
- activeInputSourceId: string?
- outboundStreamName: string
- screenCaptureConsentRequired: Boolean
- statusMessage: string?
- canStart: Boolean
- canStop: Boolean
- errorState: string?

Outputs (events):

- launchScreenCaptureConsent(inputSourceId)

Guarantees:

- Start and stop actions are guarded against duplicate concurrent requests.
- When the selected source is `device-screen:*`, start emits
  `launchScreenCaptureConsent` before repository start proceeds unless consent is
  already available.
- Interrupted state exposes retry and stop actions.

### 2.2 SourceListViewModel (interoperability extension)

Inputs:

- onSourceSelected(sourceId)
- onDeviceScreenShareSelected(hostInstanceId)
- onOpenOutputForSelectedSource()

Outputs (state/events):

- selectedSourceId: string?
- navigationEvent: OpenOutputControl(sourceId)

Guarantees:

- Selected source identity remains canonical sourceId across viewer and output
  flows, including the reserved local `device-screen:*` namespace.

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
- `OutputControlRoute.sourceId` may be a discovered source ID or a reserved
  local `device-screen:<hostInstanceId>` ID.
- Navigation remains single-activity and graph-based.
- Returning from output control to source list preserves selected source context.

## 4. Permission Contract

- No dangerous permission may be added without explicit spec amendment.
- Location permission remains prohibited for discovery/output flows.
- Screen capture must use Android `MediaProjection` consent from explicit user
  action; `RECORD_AUDIO` is out of scope unless separately approved.

## 5. Observability Contract

Required non-sensitive event categories:

- output_start_requested
- output_screen_share_consent_requested
- output_screen_share_consent_granted
- output_screen_share_consent_denied
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

Host automation inputs:

- Publisher emulator serial
- Receiver emulator serial
- App package / APK under test
- Stream name override (optional)
- Discovery, playback, and stop-propagation timeouts

Scenario requirements:

- Emulator A runs this app in publisher role and exposes an outbound NDI stream
  from its own screen via the reserved `device-screen:*` source.
- Emulator B runs this app in receiver role and uses source discovery + viewer
  flow to locate and render emulator A output.
- Both emulators must be on the same multicast-capable network segment.
- Host automation must verify both emulators are connected with `adb`, the app is
  installed on both, and publisher/receiver serials are distinct before UI
  assertions begin.
- Publisher consent must be accepted explicitly during the automated run.

Pass criteria:

- Publisher transitions to ACTIVE output state.
- Receiver discovers publisher stream within validation timeout.
- Receiver reaches PLAYING state for publisher stream.
- Stopping output on publisher transitions receiver away from active playback
  with recoverable UX.
- Evidence includes run metadata plus artifacts for both publisher and receiver.

Failure criteria:

- Receiver never discovers publisher stream within timeout.
- Receiver cannot transition to PLAYING for discovered stream.
- Publisher stop does not propagate observable playback end/interruption state.
- Publisher cannot complete capture consent or reach ACTIVE for the local
  screen-share source.

## 7. Release Validation Contract

- Any toolchain-affecting change (compileSdk/targetSdk, AGP, Gradle, Kotlin,
  Java/JBR, AndroidX, NDK/CMake, NDI SDK) requires rerunning prerequisite checks,
  unit tests, instrumentation tests, and release build validation.
- Release validation must include at least one full dual-emulator interop run
  for publish -> discover -> play -> stop regression coverage.
- The placeholder browser-only interop test is not acceptable release evidence;
  Android-device automation evidence is required.
- Toolchain blocker `TOOLCHAIN-001` (or successor) must remain documented with
  owner, affected components, and target resolution timeline until resolved.
