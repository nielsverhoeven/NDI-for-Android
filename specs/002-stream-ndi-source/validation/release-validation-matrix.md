# Release Validation Matrix

## Dual-Emulator Role Matrix

| Scenario ID | Publisher Device | Receiver Device | Network Preconditions | Expected Outcome | Evidence File |
|---|---|---|---|---|---|
| RVM-001 | Emulator A | Emulator B | Same multicast-capable segment | Publisher reaches ACTIVE and receiver reaches PLAYING | dual-emulator-e2e-report.md |
| RVM-002 | Emulator A | Emulator B | Same segment, then publisher stop | Receiver transitions out of active playback with recoverable UX | dual-emulator-e2e-report.md |
| RVM-003 | Emulator A | Emulator B | Simulated source loss during ACTIVE | Receiver and publisher expose interruption/recovery path | us3-output-recovery-validation.md |

## Validation Gates

- Unit tests pass.
- Playwright dual-emulator suite passes.
- Release hardening verification passes.
- API 24+ compatibility verification passes.
- Success criteria aggregation report is complete.
