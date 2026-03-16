# Implementation Plan: NDI Output Validation with Dual-Emulator Screen Share

**Branch**: `002-stream-ndi-source` | **Date**: 2026-03-16 | **Spec**: `specs/002-stream-ndi-source/spec.md`
**Input**: Feature specification from `/specs/002-stream-ndi-source/spec.md`

## Summary

Complete the output feature’s real validation path by replacing the current
placeholder Playwright scaffold with an automated two-emulator workflow where
emulator A publishes its own screen as an NDI stream and emulator B discovers,
opens, and renders that stream through the existing source-list and viewer
feature. The design keeps MVVM + repository boundaries intact, uses the
existing single-activity Navigation graph, models local screen share as a
reserved output source identity, isolates native send/capture work behind
`ndi/sdk-bridge`, and drives host-side end-to-end automation with Playwright
Android device control plus `adb`/PowerShell orchestration.

## Technical Context

**Language/Version**: Kotlin 2.2.10 for Android modules with Java/Kotlin bytecode target 17, TypeScript 5.8.x for Playwright automation, PowerShell 5.1+ for Windows orchestration; Gradle wrapper verified on JDK 21.0.10  
**Primary Dependencies**: AndroidX/Jetpack (Lifecycle, Navigation, Fragment, Activity, Room), Material components, Kotlin Coroutines/Flow, NDI 6 Android SDK via `ndi/sdk-bridge`, `@playwright/test` 1.53.x, Android `adb`/`emulator` CLI  
**Storage**: Room for persisted output configuration and continuity metadata; file-based Playwright reports/logcat artifacts for validation evidence  
**Testing**: JUnit4 unit/repository tests, Android instrumentation compatibility tests, Playwright Android-device end-to-end automation, PowerShell launcher for dual-emulator execution  
**Target Platform**: Android API 24+ phones/tablets, with Windows-hosted dual Android emulators on the same multicast-capable network segment  
**Android Toolchain Baseline**: `compileSdk` 34 / `targetSdk` 34 / `minSdk` 24, AGP 9.0.0, Gradle 9.2.1 wrapper, Kotlin plugin 2.2.10, Java toolchain 21 with Java 17 bytecode targets, AndroidX versions from `gradle/libs.versions.toml`, NDI 6 Android SDK compatibility through `ndi/sdk-bridge`  
**Project Type**: Feature-modularized Android mobile app with host-side E2E automation workspace  
**Performance Goals**: Publisher reaches ACTIVE within 5 seconds in >= 90% controlled runs; receiver discovers and reaches PLAYING within the spec success-criteria window; publisher stop propagates to receiver within 2 seconds in >= 95% runs; naming conflicts resolve to a unique user-visible outbound identity  
**Constraints**: Preserve `Fragment -> ViewModel -> Repository` flow; keep one active output session per app instance; bound retry window to 15 seconds; use Playwright as the default E2E framework; require explicit screen-capture user consent without adding dangerous manifest permissions; replace the current placeholder browser-based interop test with real Android-device automation; maintain release hardening and API 24+ support  
**Scale/Scope**: Two concurrent emulator instances (publisher and receiver), one reserved local screen-share source plus discovered NDI sources, three user stories already defined in spec/tasks, one mandatory publish -> discover -> play -> stop interoperability path

## Constitution Check (Pre-Design Gate)

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- MVVM gate: PASS - output, consent, and receiver state transitions remain owned by ViewModels and repositories; Fragments only launch system dialogs and render state.
- Navigation gate: PASS - single-activity Navigation Component remains the only routing mechanism; output/viewer deep links stay intact.
- Data gate: PASS - publisher automation, screen-capture consent, and receiver playback each remain repository-mediated.
- TDD gate: PASS - failing JUnit tests plus Playwright Android dual-emulator tests are explicit, and the existing placeholder interop spec is treated as incomplete coverage to replace.
- UX gate: PASS - Material status, consent, interruption, and recovery messaging remain part of the screen contracts.
- Battery gate: PASS WITH EVIDENCE TASK - screen capture/output exists only for explicit foreground operator sessions, with bounded retry and no background scheduler addition; battery-impact validation evidence is required before closure.
- Offline gate: PASS - Room continues storing only operator preferences and continuity metadata; live transport state stays transient.
- Permission gate: PASS WITH NOTE - Android screen capture uses `MediaProjection` user consent rather than new dangerous manifest permissions; any future audio capture permission would require a spec amendment.
- Modularity gate: PASS - work stays inside `app`, `feature/ndi-browser:{domain,data,presentation}`, `core:{model,database}`, `ndi/sdk-bridge`, and `testing/e2e`.
- Release gate: PASS - `verifyReleaseHardening`, release assembly, and dual-emulator evidence remain mandatory before closure.
- Platform gate: PASS - Android API 24+ compatibility and phone/tablet validation remain in scope.
- Toolchain gate: PASS WITH BLOCKER - `TOOLCHAIN-001` remains open until the current AGP/Gradle/Kotlin baseline and dual-emulator release validation are fully recorded and synchronized.

## Project Structure

### Documentation (this feature)

```text
specs/002-stream-ndi-source/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── ndi-output-feature-contract.md
├── tasks.md
└── validation/
    ├── battery-impact-report.md
    ├── dual-emulator-e2e-report.md
    ├── dual-emulator-network-preflight.md
    ├── device-layout-validation-report.md
    ├── material3-compliance-report.md
    ├── permission-justification.md
    ├── quickstart-validation-report.md
    ├── release-validation-matrix.md
    ├── success-criteria-report.md
    └── toolchain-currency-review.md
```

### Source Code (repository root)

```text
app/
├── src/main/java/com/ndi/app/di/
├── src/main/java/com/ndi/app/navigation/
└── src/main/res/navigation/

core/
├── database/
├── model/
└── testing/

feature/ndi-browser/
├── domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/
├── data/src/main/java/com/ndi/feature/ndibrowser/data/
└── presentation/src/main/java/com/ndi/feature/ndibrowser/
    ├── output/
    ├── source_list/
    └── viewer/

ndi/sdk-bridge/
└── src/main/java/com/ndi/sdkbridge/

testing/e2e/
├── scripts/run-dual-emulator-e2e.ps1
├── playwright.config.ts
└── tests/
    ├── interop-dual-emulator.spec.ts
    └── support/
```

**Structure Decision**: Use the existing feature-first Android repository
layout. UI composition and navigation stay in `app`, repositories/contracts stay
in feature modules, native transport/capture integration remains isolated to
`ndi/sdk-bridge`, and all host-driven emulator orchestration stays in
`testing/e2e`.

## Phase 0: Research Consolidation

Research outputs are finalized in `specs/002-stream-ndi-source/research.md` and
resolve the design questions relevant to the user-requested validation flow:

- Replace the current browser placeholder in `testing/e2e/tests/interop-dual-emulator.spec.ts` with real Playwright Android device automation.
- Treat emulator A screen share as a reserved local output source identity rather than a separate navigation flow.
- Use explicit `MediaProjection` consent triggered by operator action without adding dangerous manifest permissions.
- Keep the publisher/receiver run deterministic with emulator/network preflight, artifact capture, and bounded timeouts.
- Align the plan with the branch’s actual build baseline (AGP 9.0.0, Gradle 9.2.1, Kotlin 2.2.10, JDK 21) while keeping `TOOLCHAIN-001` open for validation sync.

All research decisions include rationale and alternatives considered.

## Phase 1: Design & Contracts

Design artifacts are finalized and aligned with the requested two-emulator
screen-share validation path:

- Data model: `specs/002-stream-ndi-source/data-model.md`
  - Entities include `OutputInputIdentity`, `OutputSession`,
    `OutputConfiguration`, `OutputHealthSnapshot`, and
    `DualEmulatorValidationRun`.
- Contract definition:
  `specs/002-stream-ndi-source/contracts/ndi-output-feature-contract.md`
  - Repository, consent, ViewModel, navigation, observability, and
    host-automation contracts cover publish -> discover -> play -> stop.
- Quickstart and validation workflow:
  `specs/002-stream-ndi-source/quickstart.md`
  - Documents Windows-friendly prerequisite checks, build/install commands,
    Playwright Android execution, and evidence capture expectations.
- UX/security/battery evidence artifacts:
  - `specs/002-stream-ndi-source/validation/material3-compliance-report.md`
  - `specs/002-stream-ndi-source/validation/permission-justification.md`
  - `specs/002-stream-ndi-source/validation/battery-impact-report.md`
  - Capture Material 3 verification, least-permission justification, and
    battery-impact validation required by constitution gates.
- Toolchain/governance evidence:
  `specs/002-stream-ndi-source/validation/toolchain-currency-review.md`
  - Updated to reflect the branch’s current wrapper/build baseline and the
    remaining validation blocker status.

## Constitution Check (Post-Design Re-Evaluation)

- MVVM gate: PASS - output/screen-share intent handling is modeled as ViewModel state plus repository effects.
- Navigation gate: PASS - no secondary activity architecture is introduced; `main_nav_graph.xml` remains authoritative.
- Data gate: PASS - source discovery, viewer playback, output publishing, consent, and validation recording stay behind repository contracts.
- TDD gate: PASS - JUnit + Playwright Android dual-emulator coverage is explicit and replaces placeholder scaffolding.
- UX gate: PASS - Material 3 status, consent, and recovery states are codified in the screen and ViewModel contracts.
- Battery gate: PASS WITH EVIDENCE TASK - no persistent background worker or unbounded retry loop is introduced; capture/output remain operator-bounded and battery-impact evidence is produced.
- Offline gate: PASS - only continuity/configuration data are persisted in Room.
- Permission gate: PASS WITH NOTE - no dangerous manifest permission addition is planned; `MediaProjection` consent is explicit and user-visible.
- Modularity gate: PASS - module boundaries and service-locator dependency providers remain unchanged in principle.
- Release gate: PASS - plan requires Playwright dual-emulator evidence plus release-hardening validation before completion.
- Platform gate: PASS - API 24+, phone/tablet layouts, and emulator/device interoperability remain mandatory.
- Toolchain gate: PASS WITH BLOCKER - blocker documentation now reflects the active branch baseline, but remains open until final validation completes.

## Phase 2 Readiness

Planning artifacts are complete and ready for `/speckit.tasks` execution and
implementation sequencing:

- `specs/002-stream-ndi-source/spec.md`
- `specs/002-stream-ndi-source/plan.md`
- `specs/002-stream-ndi-source/research.md`
- `specs/002-stream-ndi-source/data-model.md`
- `specs/002-stream-ndi-source/contracts/ndi-output-feature-contract.md`
- `specs/002-stream-ndi-source/quickstart.md`
- `specs/002-stream-ndi-source/validation/toolchain-currency-review.md`
- `specs/002-stream-ndi-source/tasks.md`

## Complexity Tracking

No constitution violations require an exception. The only open governance item
is blocker `TOOLCHAIN-001`, which remains tracked as validation/documentation
completion work rather than a policy waiver.
