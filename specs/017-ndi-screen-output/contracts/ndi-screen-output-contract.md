# Contract: NDI Screen Share Output Redesign

## 1. Repository Contracts

### 1.1 NdiOutputRepository

- startOutput(inputSourceId: string, streamName: string): OutputSession
- stopOutput(): OutputSession
- observeOutputSession(): stream of OutputSession
- retryInterruptedOutputWithinWindow(windowSeconds = 15): OutputSession
- observeOutputHealth(): stream of OutputHealthSnapshot

Behavioral requirements:

- At most one active output session exists per app instance.
- startOutput is idempotent while STARTING/ACTIVE.
- Local screen-share source ids use reserved namespace device-screen:<hostInstanceId>.
- Start for local screen-share requires per-session consent.
- For configured and unreachable discovery server, startOutput MUST fail without transitioning to ACTIVE, must set output state to INTERRUPTED, and return an actionable discovery error.
- For no configured discovery server, startOutput advertises via mDNS.

### 1.2 ScreenCaptureConsentRepository

- beginConsentRequest(inputSourceId: string): void
- registerConsentResult(inputSourceId: string, granted: boolean, tokenRef?: string): ScreenCaptureConsentState
- getConsentState(inputSourceId: string): ScreenCaptureConsentState?
- clearConsent(inputSourceId: string): void

Behavioral requirements:

- Consent can only be initiated from explicit user action.
- Consent denial leaves stream in recoverable non-active state with user-visible reason.
- clearConsent MUST be invoked on explicit stop to enforce re-consent on next start.

### 1.3 OutputConfigurationRepository

- savePreferredStreamName(value: string): void
- getPreferredStreamName(): string
- saveLastSelectedInputSource(sourceId: string): void
- getLastSelectedInputSource(): string?
- getConfiguration(): OutputConfiguration

Behavioral requirements:

- Stream name and last selected source persist across app restarts.
- Invalid stream names are rejected with explicit validation errors.

## 2. ViewModel Contracts

### 2.1 OutputControlViewModel

Inputs:

- onOutputScreenVisible(sourceId)
- onStreamNameChanged(value)
- onStartOutputPressed()
- onScreenCaptureConsentResult(granted, tokenRef?)
- onStopOutputPressed()
- onRetryOutputPressed()

Outputs (state):

- outputState: READY | STARTING | ACTIVE | STOPPING | STOPPED | INTERRUPTED
- sourceId: string
- streamName: string
- consentRequired: boolean
- canStart/canStop/canRetry: boolean
- errorMessage: string?

Outputs (events):

- consentPromptEvents(sourceId)
- settingsToggleEvents()

Guarantees:

- Duplicate start/stop taps are ignored safely.
- On stop, consent is cleared and subsequent start requires consent prompt.
- If configured discovery server is unreachable, state transitions to INTERRUPTED (never ACTIVE) and errorMessage is populated with actionable guidance.
- Retry remains bounded to 15 seconds.

## 3. UI Contracts

### 3.1 Output Control Screen

Required controls:

- Prominent Share Screen button in idle states.
- Stop Sharing button during STARTING/ACTIVE/INTERRUPTED states where stopping is valid.
- Stream name input with default populated value.
- State label and error message region.
- Consent explanation and interruption recovery actions.

Required behaviors:

- Output screen content is distinct from Viewer screen content.
- Persistent system notification is present while ACTIVE and includes stop action.

## 4. Discovery Contracts

- If discovery endpoint exists and is reachable, register stream with server.
- If discovery endpoint is absent, use mDNS announcement.
- If discovery endpoint exists but unreachable, fail start and surface explicit discovery connectivity error.
- On stop, withdraw stream advertisement from active discovery mode.

## 5. Validation Contracts

- Preflight command required before e2e: scripts/verify-android-prereqs.ps1
- Playwright dual-emulator suite must validate:
  - start -> consent -> active
  - stop -> re-start requires consent again
  - background continuity (home + other app)
  - discovery configured reachable path
  - discovery not configured mDNS path
  - discovery configured unreachable fails start with error
- Existing Playwright regression suites must remain passing.
