# Implementation Plan: NDI Source Network Output and Dual-Emulator End-to-End Validation

**Branch**: `002-stream-ndi-source` | **Date**: 2026-03-16 | **Spec**: `specs/002-stream-ndi-source/spec.md`
**Input**: Feature specification from `/specs/002-stream-ndi-source/spec.md`

## Summary

Add an outbound NDI publishing flow that lets one app instance stream a selected
source to the local network, then validate end-to-end interoperability with the
existing discovery/viewer feature by running two Android emulators on the same
network: emulator A publishes its own screen as NDI output and emulator B
discovers and plays that stream. Architecture remains MVVM + repository-mediated
with native NDI integration isolated in `ndi/sdk-bridge`, strict TDD, and
Playwright as the default end-to-end framework.

## Technical Context

**Language/Version**: Kotlin 1.9.24 (Android modules target Java 17); Gradle runtime on Android Studio stable JBR 21  
**Primary Dependencies**: AndroidX/Jetpack (Lifecycle, Navigation, Room), Material 3, Kotlin Coroutines/Flow, NDI 6 Android SDK through `ndi/sdk-bridge`  
**Storage**: Room for persisted output configuration, continuity state, and recent output session metadata  
**Testing**: JUnit for unit and repository-contract tests; Playwright as default end-to-end framework for dual-emulator interop; connected Android tests for platform compatibility verification where applicable  
**Target Platform**: Android API 24+ phones/tablets; dual Android emulators on one host for E2E interoperability validation  
**Android Toolchain Baseline**: compileSdk 34 / targetSdk 34, AGP 8.5.2, Gradle 8.7, Kotlin 1.9.24, Java 17 module targets, Android Studio stable JBR 21; uplift remains tracked under `TOOLCHAIN-001` until compatible NDI validation is complete  
**Project Type**: Feature-modularized Android mobile app with native SDK bridge and Playwright-led E2E system validation  
**Performance Goals**: Start outbound output within 5s in >= 90% attempts; stop output within 2s in >= 95% attempts; dual-emulator publish->discover->play pass in >= 90% validation runs  
**Constraints**: Local-network topology only; one active outbound session per app instance; duplicate start/stop prevention; no speculative background jobs; no unjustified new permissions; compatibility with existing viewer flow  
**Scale/Scope**: Three user stories (start output, control/monitor, interruption recovery) plus mandatory cross-feature E2E topology using two emulators running this app in distinct roles

## Constitution Check (Pre-Design Gate)

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- MVVM gate: PASS - output/session orchestration is delegated to ViewModels while UI remains render + intent dispatch only.
- Navigation gate: PASS - single-activity Navigation Component continues to own source list, viewer, and output control routing.
- Data gate: PASS - output config/session state stays behind repository interfaces; no direct persistence or native bridge calls from UI.
- TDD gate: PASS - tests-first plan includes unit, repository-contract, and Playwright-default E2E coverage. Any non-Playwright E2E path requires approved exception documentation.
- UX gate: PASS - Material 3 states are defined for ready/starting/active/stopping/interrupted output UX.
- Battery gate: PASS - output work is user-initiated and bounded; no new uncontrolled background loops.
- Offline gate: PASS - Room persists continuity state (stream-name preference, last source/output context) for resilient relaunch UX.
- Permission gate: PASS - no new dangerous permissions planned without explicit spec amendment and justification artifact.
- Modularity gate: PASS - changes remain in `feature/ndi-browser` and `ndi/sdk-bridge` with app wiring in `app` only.
- Release gate: PASS - release validation with shrinking/optimization and Playwright E2E evidence is planned.
- Platform gate: PASS - API 24+ phone/tablet support and dual-emulator interoperability validation are explicit.
- Toolchain gate: PASS WITH BLOCKER - compileSdk/targetSdk, AGP, Gradle, Kotlin, JDK/JBR, AndroidX/Jetpack, NDK/CMake, and NDI SDK compatibility remain documented with blocker `TOOLCHAIN-001` tracked to closure.

## Project Structure

### Documentation (this feature)

```text
specs/002-stream-ndi-source/
|-- plan.md
|-- research.md
|-- data-model.md
|-- quickstart.md
|-- contracts/
|   `-- ndi-output-feature-contract.md
`-- tasks.md
```

### Source Code (repository root)

```text
app/
core/
feature/
ndi/
testing/e2e/
scripts/
docs/
```

**Structure Decision**: Keep the existing multi-module Android architecture.
App-level navigation and composition remain in `app`, business contracts and
implementations stay in `feature/ndi-browser/{domain,data,presentation}`,
persistent entities stay in `core/database`, native NDI sender/receiver
integration remains encapsulated in `ndi/sdk-bridge`, and Playwright E2E
automation assets live in `testing/e2e`.

## Phase 0: Research Consolidation

Research outputs are finalized in `specs/002-stream-ndi-source/research.md` and
resolve technical decisions for:

- NDI outbound publishing strategy through the native bridge boundary.
- Output lifecycle ownership and repository contract boundaries.
- Dual-emulator same-network E2E topology and pass/fail criteria.
- Start/stop idempotency and interruption-recovery behavior.
- Governance continuity for Playwright-default E2E and exception workflow.
- Toolchain compatibility tracking through `TOOLCHAIN-001`.

## Phase 1: Design & Contracts

Design artifacts are finalized and aligned with the specification:

- Data model: `specs/002-stream-ndi-source/data-model.md`
- Contract definition: `specs/002-stream-ndi-source/contracts/ndi-output-feature-contract.md`
- Quickstart: `specs/002-stream-ndi-source/quickstart.md`

Design scope includes repository and ViewModel contracts for output lifecycle,
cross-feature publish->discover->capture E2E verification contract, and
dual-emulator validation guidance.

## Constitution Check (Post-Design Re-Evaluation)

- MVVM gate: PASS - design keeps session and state transitions in ViewModels.
- Navigation gate: PASS - no multi-activity deviations; route contracts stay centralized.
- Data gate: PASS - repositories mediate output, telemetry, and persistence.
- TDD gate: PASS - dual-emulator E2E coverage is explicit and Playwright-default policy is enforced.
- UX gate: PASS - Material 3 status/error patterns are codified in contract outputs.
- Battery gate: PASS - output starts/stops only via explicit user action; retry remains bounded.
- Offline gate: PASS - Room-backed continuity for output defaults and recent state is defined.
- Permission gate: PASS - no new dangerous permissions introduced by design.
- Modularity gate: PASS - module boundaries mirror existing architecture authority.
- Release gate: PASS - release-mode validation includes outbound stream regression, R8/proguard checks, and Playwright E2E evidence.
- Platform gate: PASS - API 24+ plus phone/tablet and dual-emulator validation remain required.
- Toolchain gate: PASS WITH BLOCKER - compatibility uplift remains tracked and must be revalidated at release-readiness checkpoint.

## Phase 2 Readiness

Planning artifacts are complete and ready for `/speckit.tasks` execution:

- `specs/002-stream-ndi-source/spec.md`
- `specs/002-stream-ndi-source/research.md`
- `specs/002-stream-ndi-source/data-model.md`
- `specs/002-stream-ndi-source/contracts/ndi-output-feature-contract.md`
- `specs/002-stream-ndi-source/quickstart.md`
- `specs/002-stream-ndi-source/plan.md`

## Complexity Tracking

No constitution violations require policy waivers.

Tracked governance risk:

| Violation | Why Needed | Simpler Alternative Rejected Because |
| --------- | ---------- | ----------------------------------- |
| Toolchain blocker `TOOLCHAIN-001` remains open | NDI SDK compatibility validation with newer baseline is still in progress | Upgrading baseline without validated NDI compatibility would risk release instability |
