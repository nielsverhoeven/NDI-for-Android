# Contract: Dual-Emulator NDI Latency Validation

## 1. Scope Contract

- This feature defines the latency-validation scenario and quality-gate obligations for dual-emulator NDI playback measurement.
- The contract covers test orchestration and evidence requirements; no production API contract changes are introduced.

## 2. Scenario Execution Contract

### 2.1 Required Step Sequence

The latency scenario MUST execute in this order:
1. Start NDI stream output on Emulator A.
2. Open NDI stream view on Emulator B.
3. Start recording on both emulators.
4. Start random YouTube playback on Emulator A.
5. Verify active playback visibility on Emulator B through NDI viewer.
6. Analyze both recordings and compute latency.

### 2.2 Validity Rules

- Latency result is valid only when both recordings are usable and receiver playback verification succeeds.
- Runs with missing recordings, non-playing receiver state, or analysis failure are invalid and MUST not emit valid latency numbers.

## 3. Latency Method Contract

- Primary method MUST be motion/content cross-correlation between source and receiver recordings.
- Analysis output MUST include method identifier and validity state.

## 4. Quality-Gate Contract

### 4.1 New Scenario Gate

- New latency scenario MUST pass on required emulator profile(s) for the run type.

### 4.2 Existing Regression Preservation

- Existing end-to-end regression suite execution remains mandatory in the same validation cycle.
- Any regression failure or incomplete run fails the gate.

## 5. Evidence Contract

Each run MUST publish:
- Source recording artifact path.
- Receiver recording artifact path.
- Latency analysis output artifact path.
- Step-level checkpoint summary with first failed step when applicable.
- Regression outcome summary for existing suite.

## 6. Failure and Exception Contract

- INCOMPLETE and FAILED runs are non-compliant for merge/release gates.
- Exceptions require explicit documented waiver metadata and rerun plan.
- Exceptions do not remove the requirement to restore compliant evidence before completion.
