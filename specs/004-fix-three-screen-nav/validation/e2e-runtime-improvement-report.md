# E2E Runtime Improvement Report (SC-006)

Date: 2026-03-17
Feature: 004-fix-three-screen-nav
Criterion: SC-006 Median dual-emulator runtime improvement >=25%

## Method

Baseline-parity command used for both datasets:

```powershell
powershell -ExecutionPolicy Bypass -File .\testing\e2e\scripts\run-dual-emulator-e2e.ps1 -EmulatorASerial emulator-5554 -EmulatorBSerial emulator-5556
```

Preparation and blocker resolution:

1. Initial benchmark attempt failed because `com.ndi.app.debug` was not installed on emulator-5554.
2. Installed debug app with:

```powershell
./gradlew.bat :app:installDebug
```

3. Re-ran benchmark loops after installation.

Run count:

- Baseline-parity: 5 consecutive successful runs
- Post-change: 5 consecutive successful runs

## Baseline-Parity Runs

| Run | Start (UTC) | End (UTC) | Runtime (s) | Result |
|-----|-------------|-----------|-------------|--------|
| 1 | 2026-03-17T18:26:48Z | 2026-03-17T18:29:21Z | 153.24 | PASS |
| 2 | 2026-03-17T18:29:21Z | 2026-03-17T18:31:43Z | 142.12 | PASS |
| 3 | 2026-03-17T18:31:43Z | 2026-03-17T18:33:59Z | 135.73 | PASS |
| 4 | 2026-03-17T18:33:59Z | 2026-03-17T18:36:17Z | 137.74 | PASS |
| 5 | 2026-03-17T18:36:17Z | 2026-03-17T18:38:35Z | 138.64 | PASS |

Baseline-parity summary:

- Pass rate: 100.00% (5/5)
- Median runtime: 138.64s
- Mean runtime: 141.49s
- Min runtime: 135.73s
- Max runtime: 153.24s

## Post-Change Runs

| Run | Start (UTC) | End (UTC) | Runtime (s) | Result |
|-----|-------------|-----------|-------------|--------|
| 1 | 2026-03-17T18:38:49Z | 2026-03-17T18:41:11Z | 142.04 | PASS |
| 2 | 2026-03-17T18:41:11Z | 2026-03-17T18:43:29Z | 137.70 | PASS |
| 3 | 2026-03-17T18:43:29Z | 2026-03-17T18:45:57Z | 148.20 | PASS |
| 4 | 2026-03-17T18:45:57Z | 2026-03-17T18:48:17Z | 139.86 | PASS |
| 5 | 2026-03-17T18:48:17Z | 2026-03-17T18:50:38Z | 140.79 | PASS |

Post-change summary:

- Pass rate: 100.00% (5/5)
- Median runtime: 140.79s
- Mean runtime: 141.72s
- Min runtime: 137.70s
- Max runtime: 148.20s

## Improvement Calculation

Formula:

$$
\text{improvement \%} = \frac{\text{baseline median} - \text{post-change median}}{\text{baseline median}} \times 100
$$

Computed value:

$$
\frac{138.64 - 140.79}{138.64} \times 100 = -1.55\%
$$

## SC-006 Result

FAIL. Required threshold is >=25% median runtime improvement, but measured improvement is -1.55% (median regression).
