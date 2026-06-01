# Implementation Plan: Dual-Emulator NDI Latency Measurement

**Branch**: `009-measure-ndi-latency` | **Date**: 2026-03-20 | **Spec**: `specs/009-measure-ndi-latency/spec.md`
**Input**: Feature specification from `/specs/009-measure-ndi-latency/spec.md`

## Summary

Implement a deterministic Playwright-based dual-emulator latency scenario that:
starts NDI output on Emulator A, opens the stream on Emulator B, records both
screens, starts random YouTube playback on Emulator A, verifies active playback
on Emulator B, and computes end-to-end latency using motion/content
cross-correlation. The implementation reuses the existing `testing/e2e` harness,
adds latency-analysis artifacts, and keeps the existing regression gate intact.

## Technical Context

**Language/Version**: TypeScript (Playwright harness), Kotlin (Android app under test), PowerShell (orchestration scripts)  
**Primary Dependencies**: `@playwright/test`, `adb`/Android Emulator tooling, existing `testing/e2e/tests/support` helpers, image/video processing utilities already used by e2e harness or lightweight additions  
**Storage**: File-based artifacts under `testing/e2e/artifacts`, `testing/e2e/test-results`, and summarized evidence in `test-results/android-test-results.md`  
**Testing**: Playwright end-to-end tests on emulator(s), existing regression suite enforcement, unit tests for new latency-analysis helper logic  
**Target Platform**: Android emulators (publisher + receiver), API profiles currently used by project e2e flows  
**Project Type**: Android mobile app with external Playwright e2e validation harness  
**Performance Goals**: Successful latency scenario completes in under 10 minutes; latency output produced for successful runs  
**Constraints**: Must fail fast on incomplete playback/recording; must not weaken existing e2e regression gate; deterministic step checkpoints required  
**Scale/Scope**: One new dual-emulator scenario family plus supporting analysis artifacts and pipeline wiring updates in `testing/e2e`

## Constitution Check (Pre-Design Gate)

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] MVVM-only presentation logic enforced (no UI/business logic leakage)
- [x] Single-activity navigation compliance maintained
- [x] Repository-mediated data access preserved
- [x] TDD evidence planned (Red-Green-Refactor with failing-test-first path)
- [x] Unit test scope defined using JUnit
- [x] Playwright e2e scope defined for end-to-end flows
- [x] For visual UI additions/changes: emulator Playwright e2e tests are explicitly planned
- [x] For visual UI additions/changes: existing Playwright e2e regression run is explicitly planned
- [x] Material 3 compliance verification planned for UI changes
- [x] Battery/background execution impact evaluated
- [x] Offline-first and Room persistence constraints respected (if applicable)
- [x] Least-permission/security implications documented
- [x] Feature-module boundary compliance documented
- [x] Release hardening validation planned (R8/ProGuard + shrink resources)

## Project Structure

### Documentation (this feature)

```text
specs/009-measure-ndi-latency/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── ndi-latency-validation-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
testing/e2e/
├── scripts/
├── tests/
│   └── support/
└── artifacts/

feature/ndi-browser/
├── presentation/
├── data/
└── domain/

app/
└── src/main/

test-results/
└── android-test-results.md
```

**Structure Decision**: Keep production app architecture unchanged and implement this feature primarily in the existing `testing/e2e` automation layer, with artifact/report updates in `test-results` and no cross-module architecture violations.

## Phase 0: Research Consolidation

Research output is captured in `specs/009-measure-ndi-latency/research.md` and resolves:

- Latency calculation method: motion/content cross-correlation (from clarification).
- Recording start ordering and determinism requirements.
- Validity rules for failed/partial measurement runs.
- Evidence and reproducibility requirements for CI and triage.
- Preservation of existing regression gate behavior.

All technical context items are resolved; no `NEEDS CLARIFICATION` markers remain.

## Phase 1: Design & Contracts

Design artifacts generated for this plan:

- Data model: `specs/009-measure-ndi-latency/data-model.md`
- Contract: `specs/009-measure-ndi-latency/contracts/ndi-latency-validation-contract.md`
- Quickstart: `specs/009-measure-ndi-latency/quickstart.md`

## Constitution Check (Post-Design Re-Evaluation)

- MVVM gate: PASS - feature scope remains in e2e harness; no presentation/business leakage.
- Navigation gate: PASS - validates existing navigation routes without introducing new app navigation topology.
- Repository gate: PASS - no direct data-layer bypass introduced.
- TDD gate: PASS - plan includes failing-test-first sequencing for new scenario and helper logic.
- Playwright gate: PASS - emulator-based e2e coverage is core scope.
- Material 3 gate: PASS - no new app UI components added; verification stays user-outcome driven.
- Battery gate: PASS - no persistent runtime/background feature changes.
- Offline gate: PASS - no new persistence model required beyond existing app behavior.
- Security gate: PASS - no new permissions; artifact handling avoids sensitive payload logging.
- Modularization gate: PASS - test harness changes isolated to `testing/e2e` with existing module boundaries preserved.
- Release gate: PASS - release hardening remains unchanged and still required.

## Phase 2 Readiness

Ready inputs for `/speckit.tasks`:

- `specs/009-measure-ndi-latency/spec.md`
- `specs/009-measure-ndi-latency/plan.md`
- `specs/009-measure-ndi-latency/research.md`
- `specs/009-measure-ndi-latency/data-model.md`
- `specs/009-measure-ndi-latency/contracts/ndi-latency-validation-contract.md`
- `specs/009-measure-ndi-latency/quickstart.md`

## Complexity Tracking

No constitution violations require an exception.
