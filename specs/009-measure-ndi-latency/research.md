# Phase 0 Research: Dual-Emulator NDI Latency Measurement

## Decision 1: Use motion/content cross-correlation as the primary latency method

- Decision: Compute end-to-end latency by correlating visual motion/content between source and receiver recordings.
- Rationale: Works with real UI playback and does not require app instrumentation or frame timestamp overlays.
- Alternatives considered:
  - Overlay timestamp injection (rejected: extra rendering dependency and possible UI contamination).
  - App-side event timestamps only (rejected: does not directly measure visual latency seen by user).

## Decision 2: Keep deterministic step checkpoints with fail-fast behavior

- Decision: Use explicit scenario checkpoints for stream start, receiver visibility, recording start, playback start, and analysis completion.
- Rationale: Improves triage and prevents long opaque timeouts.
- Alternatives considered:
  - Single terminal pass/fail only (rejected: low diagnostic value).

## Decision 3: Start both recordings before source playback trigger

- Decision: Recording on Emulator A and B begins before launching random YouTube playback.
- Rationale: Ensures the analysis window includes playback onset on both ends.
- Alternatives considered:
  - Start recording after playback begins (rejected: risks missing sync onset and invalid latency samples).

## Decision 4: Require objective receiver-playing validation before latency analysis

- Decision: The scenario must verify active visual playback on Emulator B before producing latency output.
- Rationale: Prevents invalid latency numbers from frozen or non-playing receiver states.
- Alternatives considered:
  - Assume playback based on navigation state only (rejected: frequent false positives).

## Decision 5: Persist analysis evidence as first-class artifacts

- Decision: Every run stores source/receiver recordings plus machine-readable latency analysis output.
- Rationale: Enables reproducibility, auditability, and regression investigation.
- Alternatives considered:
  - Keep only summary logs (rejected: insufficient for post-failure diagnosis).

## Decision 6: Preserve existing regression gate in same validation cycle

- Decision: Existing end-to-end regression suites remain mandatory whenever this feature scenario runs in quality gates.
- Rationale: New measurement capability must not reduce previous coverage confidence.
- Alternatives considered:
  - Run only new latency scenario (rejected: regression risk).

## Clarification Resolution Status

All current high-impact ambiguities are resolved for planning. No additional blocking clarifications remain.
