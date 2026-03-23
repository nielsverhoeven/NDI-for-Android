# E2E Runtime Report

**Feature**: Background Stream Persistence (005)  
**Run Date**: 2026-03-20  
**Branch**: `005-background-stream-persistence`  
**Harness**: `testing/e2e/scripts/run-dual-emulator-e2e.ps1`

---

## Run Summary

| Scenario | Result | Duration |
|----------|--------|----------|
| `@dual-emulator publish discover play stop interop` | PASS | ~2.4 min |
| `@dual-emulator restart output with new stream name remains discoverable` | PASS | ~2.6 min |
| **Total** | **2/2 PASS** | **5.0 min** |

---

## Artifact Directory

```
testing/e2e/artifacts/dual-emulator-20260320-141359/
├── android-version-diagnostics.json
├── publisher-preflight.log
├── publisher-postrun.log
├── receiver-preflight.log
├── receiver-postrun.log
├── scenario-checkpoints.json
└── screenshots/
    ├── publisher-preflight.png
    ├── publisher-active.png
    ├── publisher-chrome.png
    ├── publisher-nos.png
    ├── publisher-final.png
    ├── publisher-postrun.png
    ├── receiver-preflight.png
    ├── receiver-before-play.png
    ├── receiver-playing.png
    ├── receiver-chrome.png
    ├── receiver-nos.png
    ├── receiver-final.png
    ├── receiver-postrun.png
    ├── restart-publisher-first-active.png
    ├── restart-publisher-second-active.png
    ├── restart-receiver-first-before-play.png
    ├── restart-receiver-first-playing.png
    ├── restart-receiver-second-before-play.png
    └── restart-receiver-second-playing.png
```

---

## Checkpoint Timeline (Scenario 1)

All 6 steps passed in order:

1. `START_STREAM_A` — PASSED  
2. `START_VIEW_B` — PASSED  
3. `OPEN_CHROME_A` — PASSED  
4. `VERIFY_CHROME_ON_B` — PASSED  
5. `OPEN_NOS_A` — PASSED  
6. `VERIFY_NOS_ON_B` — PASSED  

Full timeline: `testing/e2e/artifacts/dual-emulator-20260320-141359/scenario-checkpoints.json`

---

## Visual Evidence Notes

- `receiver-playing.png`: Emulator B viewer surface shows non-black, non-zero-delta content matching publisher active frame.
- `receiver-chrome.png`: Emulator B viewer surface updated after broadcaster navigated to Chrome. Similarity ≥ 0.62 to `publisher-chrome.png`.
- `receiver-nos.png`: Emulator B viewer surface updated after broadcaster navigated to nos.nl. Similarity ≥ 0.62 to `publisher-nos.png`.
- `restart-*` screenshots: Both sessions (first and second stream name) show PLAYING state with visible content matching the respective publisher reference frame.

---

## Android Version Diagnostics

Full diagnostics at `testing/e2e/artifacts/dual-emulator-20260320-141359/android-version-diagnostics.json`.

Both devices were confirmed within the rolling latest-five support window.
