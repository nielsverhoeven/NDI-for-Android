# Dual-Emulator Runtime Baseline (Pre-Change)

Date: 2026-03-17
Feature: 004-fix-three-screen-nav
Environment: Windows host, dual Android emulators, Playwright unified suite

## Baseline Method

Use one command path for baseline parity:

```powershell
powershell -ExecutionPolicy Bypass -File .\testing\e2e\scripts\run-dual-emulator-e2e.ps1 -EmulatorASerial emulator-5554 -EmulatorBSerial emulator-5556
```

Capture at least 5 consecutive successful runs and compute median runtime.

## Recorded Runs

| Run | Start (UTC) | End (UTC) | Runtime (s) | Result |
|-----|-------------|-----------|-------------|--------|
| 1 | 2026-03-17T18:26:48Z | 2026-03-17T18:29:21Z | 153.24 | PASS |
| 2 | 2026-03-17T18:29:21Z | 2026-03-17T18:31:43Z | 142.12 | PASS |
| 3 | 2026-03-17T18:31:43Z | 2026-03-17T18:33:59Z | 135.73 | PASS |
| 4 | 2026-03-17T18:33:59Z | 2026-03-17T18:36:17Z | 137.74 | PASS |
| 5 | 2026-03-17T18:36:17Z | 2026-03-17T18:38:35Z | 138.64 | PASS |

## Baseline Summary

- Median runtime: 138.64 seconds
- Mean runtime: 141.49 seconds
- Min runtime: 135.73 seconds
- Max runtime: 153.24 seconds

## Notes

This file is intentionally frozen as the pre-change baseline reference and should be used for SC-006 parity comparison in T044.
