# US1 Viewer-Back Reliability Matrix (SC-002)

Date: 2026-03-17
Feature: 004-fix-three-screen-nav
Criterion: SC-002 Viewer/View back reliability >=98%

## Method

Repeated-run reliability matrix using targeted back-policy tests.

Command used:

```powershell
./gradlew.bat :app:testDebugUnitTest --tests "com.ndi.app.navigation.TopLevelNavViewModelTest.onBackPressed_fromViewerVisible_consumesAndNavigatesToViewRoot" --tests "com.ndi.app.navigation.TopLevelNavViewModelTest.onBackPressed_fromViewRoot_consumesAndNavigatesHome"
```

Run count: 20 consecutive runs

## Repeated Runs

| Run | Start (UTC) | End (UTC) | Duration (s) | Result |
|-----|-------------|-----------|--------------|--------|
| 1 | 2026-03-17T18:23:38Z | 2026-03-17T18:23:44Z | 5.49 | PASS |
| 2 | 2026-03-17T18:23:44Z | 2026-03-17T18:23:49Z | 4.93 | PASS |
| 3 | 2026-03-17T18:23:49Z | 2026-03-17T18:23:54Z | 4.97 | PASS |
| 4 | 2026-03-17T18:23:54Z | 2026-03-17T18:23:59Z | 4.98 | PASS |
| 5 | 2026-03-17T18:23:59Z | 2026-03-17T18:24:04Z | 5.03 | PASS |
| 6 | 2026-03-17T18:24:04Z | 2026-03-17T18:24:08Z | 4.71 | PASS |
| 7 | 2026-03-17T18:24:08Z | 2026-03-17T18:24:13Z | 5.08 | PASS |
| 8 | 2026-03-17T18:24:13Z | 2026-03-17T18:24:18Z | 4.95 | PASS |
| 9 | 2026-03-17T18:24:18Z | 2026-03-17T18:24:23Z | 4.95 | PASS |
| 10 | 2026-03-17T18:24:23Z | 2026-03-17T18:24:28Z | 4.65 | PASS |
| 11 | 2026-03-17T18:24:28Z | 2026-03-17T18:24:33Z | 4.81 | PASS |
| 12 | 2026-03-17T18:24:33Z | 2026-03-17T18:24:37Z | 4.66 | PASS |
| 13 | 2026-03-17T18:24:37Z | 2026-03-17T18:24:43Z | 5.32 | PASS |
| 14 | 2026-03-17T18:24:43Z | 2026-03-17T18:24:48Z | 4.98 | PASS |
| 15 | 2026-03-17T18:24:48Z | 2026-03-17T18:24:52Z | 4.61 | PASS |
| 16 | 2026-03-17T18:24:52Z | 2026-03-17T18:24:57Z | 4.88 | PASS |
| 17 | 2026-03-17T18:24:57Z | 2026-03-17T18:25:02Z | 5.09 | PASS |
| 18 | 2026-03-17T18:25:02Z | 2026-03-17T18:25:07Z | 4.86 | PASS |
| 19 | 2026-03-17T18:25:07Z | 2026-03-17T18:25:12Z | 4.94 | PASS |
| 20 | 2026-03-17T18:25:12Z | 2026-03-17T18:25:17Z | 4.68 | PASS |

## Summary

- Total runs: 20
- Passes: 20
- Fails: 0
- Pass rate: 100.00%
- Median duration: 4.94s
- Mean duration: 4.93s
- Min duration: 4.61s
- Max duration: 5.49s

## SC-002 Result

PASS. Reliability threshold is >=98%; measured pass rate is 100.00%.
