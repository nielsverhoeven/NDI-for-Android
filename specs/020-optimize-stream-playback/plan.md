# Implementation Plan: Optimize NDI Stream Playback with Quality Controls

**Branch**: 020-optimize-stream-playback | **Date**: March 28, 2026 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from specs/020-optimize-stream-playback/spec.md

## Summary

Optimize NDI stream playback on Android Viewer screen by delivering:

- P1: Smooth default playback at 24+ fps with automatic quality degradation and disconnection recovery.
- P2: Auto-fit player scaling that preserves aspect ratio across orientations.
- P3: User-selectable quality presets (Smooth, Balanced, High Quality) persisted in SharedPreferences.

## Technical Context

**Language/Version**: Kotlin 2.2.10 / Android API 34+
**Primary Dependencies**: NDI SDK bridge, AndroidX/Jetpack (Lifecycle, Navigation, Compose UI), Coroutines
**Storage**: Android SharedPreferences (device-scoped, non-encrypted) for quality preference persistence
**Testing**: JUnit 5 + Mockito (unit), Playwright (e2e, emulator and dual-emulator)
**Target Platform**: Android 14+ (compileSdk 34, minSdk 26)
**Project Type**: Mobile app (single-activity, multi-module feature architecture)
**Performance Goals**: 24+ fps sustained playback, 90%+ player area utilization, disconnect dialog visible within 2 seconds
**Constraints**: Battery-conscious execution, no new permissions, graceful degradation over stalls

## Constitution Check

GATE: Must pass before implementation and re-check after design/task updates.

- [x] MVVM-only presentation logic
- [x] Single-activity navigation integration
- [x] Repository-mediated data access
- [x] Strict TDD with failing-test-first sequencing
- [x] Playwright e2e coverage for changed visual behavior
- [x] Existing Playwright regression execution preserved
- [x] State persistence regression coverage included
- [x] Material 3 compliance planned
- [x] Battery-conscious behavior maintained
- [x] Offline reliability constraints respected
- [x] Least-permission security maintained
- [x] Feature-module boundaries preserved
- [x] Release hardening validation included
- [x] Runtime preflight checks included
- [x] Environment-blocked evidence capture included

No constitution violations identified.

## Project Structure

### Documentation

specs/020-optimize-stream-playback/

- spec.md
- plan.md
- research.md
- data-model.md
- quickstart.md
- contracts/quality-profile-contract.md
- contracts/player-scaling-contract.md
- tasks.md
- clarification-report.md

### Source Code (Planned)

feature/ndi-browser/

- domain/repository/NdiRepositories.kt (extend viewer contract)
- domain/repository/QualityProfileRepository.kt (new contract)
- data/repository/NdiViewerRepositoryImpl.kt (quality/degradation behavior)
- data/repository/QualityProfileRepositoryImpl.kt (new implementation)
- data/local/SharedPreferencesQualityStore.kt (new persistence adapter)
- data/model/PlaybackOptimization.kt (new)
- data/model/QualityPreference.kt (new)
- data/model/DisconnectionEvent.kt (new)
- presentation/viewer/ViewerViewModel.kt (quality and reconnect orchestration)
- presentation/viewer/ViewerScreen.kt (quality UI state and disconnect dialog)
- presentation/viewer/ViewerRecoveryTelemetry.kt (quality/recovery events)
- presentation/viewer/ViewerTelemetry.kt (dependency wiring updates via ViewerDependencies object)
- presentation/viewer/PlayerScalingState.kt (new)
- presentation/viewer/PlayerScalingCalculator.kt (new)
- presentation/viewer/PlayerScalingCalculatorImpl.kt (new)
- presentation/viewer/PlayerScalingViewModel.kt (new)
- presentation/res/layout/fragment_viewer.xml (auto-fit layout updates)
- presentation/res/menu/viewer_menu.xml (quality entry)
- presentation/res/values/strings.xml (accessible labels)

ndi/sdk-bridge/

- src/main/java/com/ndi/sdkbridge/NdiNativeBridge.kt (quality/profile APIs)
- src/main/cpp/ndi_bridge.cpp (native receiver quality/degradation updates)
- src/main/cpp/ndi_screen_share.cpp (if native behavior split requires updates)
- src/main/cpp/CMakeLists.txt (native linkage updates)

app/

- src/main/java/com/ndi/app/di/AppGraph.kt (wiring for quality repository)

Structure Decision:

- domain: add explicit quality contract and extend existing viewer contract.
- data: implement profile persistence and optimization state.
- presentation: add quality UI, scaling behavior, and recovery state handling.
- sdk-bridge: extend native bridge interfaces for runtime profile application.

## Phase Status

### Phase 0: Research and Technical Analysis

Status: COMPLETE (March 28, 2026)

Completed outputs:

- research.md with validated stack, architecture choices, risk analysis, and test strategy.

Frozen decisions:

- SharedPreferences for quality preference persistence.
- Tiered quality profiles for codec and resolution strategy.
- Aspect-ratio-preserving fit-to-bounds scaling.
- Dialog-led disconnection recovery with bounded retries.

### Phase 1: Design and Contracts

Status: COMPLETE (March 28, 2026)

Completed outputs:

- data-model.md
- contracts/quality-profile-contract.md
- contracts/player-scaling-contract.md
- quickstart.md

Design consistency notes:

- Native bridge interfaces are intentionally extended in this feature.
- Viewer dependencies are updated through existing ViewerDependencies object in ViewerTelemetry.kt.

## Next Step

Run task decomposition and then implement in priority order:

- Command: /speckit.tasks
- Delivery order: P1 then P2 then P3, with full preflight and Playwright regressions at each gate.

## Complexity Tracking

No unresolved constitution exceptions. Remaining work is implementation and validation execution against tasks.md.
