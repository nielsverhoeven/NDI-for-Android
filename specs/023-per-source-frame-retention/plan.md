# Implementation Plan: Per-Source Last Frame Retention

**Branch**: `023-per-source-frame-retention` | **Date**: 2026-03-30 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/023-per-source-frame-retention/spec.md`

## Summary

Replace the current single-slot global last-frame store with a session-scoped, in-memory LRU map that retains the last captured frame (at thumbnail resolution ~320×180) independently for each NDI source, keyed by the NDI SDK-assigned opaque source ID, capped at 10 concurrent entries. The `SourceListViewModel` will observe this map and surface the per-source thumbnail in each row. No Room schema changes are required — the feature is entirely in-memory and discarded on process end. The existing `ViewerContinuityRepository` single-source restore path is preserved unchanged.

## Technical Context

**Language/Version**: Kotlin 2.2.10 (JVM target 17)
**Primary Dependencies**: AndroidX Lifecycle / ViewModel, Kotlin Coroutines + StateFlow, Android Bitmap API
**Storage**: In-memory only (session-scoped `LinkedHashMap` LRU, no Room) — no DB migration needed
**Testing**: JUnit 5 + Mockito (unit), Playwright e2e on dual emulators
**Target Platform**: Android API 24+, minSdk 24, targetSdk/compileSdk 34
**Project Type**: Android multi-module app (feature-first Gradle modularization)
**Performance Goals**: Source list scroll remains ≥60 fps; frame capture and thumbnail scaling must complete on IO dispatcher within one frame cycle
**Constraints**: ≤10 retained frames in memory at once; thumbnail resolution ~320×180; no disk persistence beyond the session cache directory; no ANR — all Bitmap work on Dispatchers.IO
**Scale/Scope**: NDI studio environments — typically 2–20 visible sources; 10-source LRU cap covers the large majority of real workflows

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] MVVM-only presentation logic enforced — new `PerSourceFrameRepository` is domain/data only; `SourceListViewModel` collects from it; no logic in Fragment
- [x] Single-activity navigation compliance maintained — no new destinations or fragments added
- [x] Repository-mediated data access preserved — `SourceListViewModel` accesses frames only via `PerSourceFrameRepository`; no direct bitmap manipulation in ViewModel
- [x] TDD evidence planned (Red-Green-Refactor with failing-test-first path) — failing unit tests for `PerSourceFrameRepositoryImpl` (LRU eviction, keying, cap) written first
- [x] Unit test scope defined using JUnit — `PerSourceFrameRepositoryImpl`, `SourceListViewModel` map-observation behaviour
- [x] Playwright e2e scope defined for end-to-end flows — multi-source frame retention flow on dual emulators
- [x] For visual UI additions/changes: emulator Playwright e2e tests are explicitly planned — source list thumbnail per-source validation
- [x] For visual UI additions/changes: existing Playwright e2e regression run is explicitly planned — full existing suite must pass
- [x] For shared persistence/settings changes: regression tests for state-preservation — `ViewerContinuityRepository` single-source restore path not changed; regression test confirms it still works
- [x] Material 3 compliance verification planned — no new UI components; existing `sourcePreviewImage` ImageView already in layout; no Material 3 concern
- [x] Battery/background execution impact evaluated — in-memory only; no background work; frame captured only at viewer exit (IO dispatcher); negligible impact
- [x] Offline-first and Room persistence constraints respected — feature is intentionally session-scoped in-memory; no offline persistence needed for thumbnails
- [x] Least-permission/security implications documented — no new Android permissions required; thumbnail files written to app-private cache directory
- [x] Feature-module boundary compliance documented — new `PerSourceFrameRepository` interface in `:feature:ndi-browser:domain`; impl in `:feature:ndi-browser:data`; app wiring in `:app` `AppGraph`
- [x] Release hardening validation planned (R8/ProGuard + shrink resources) — `verifyReleaseHardening` gate confirmed in CI; no new classes that need keep-rules
- [x] Runtime preflight checks are defined — `scripts/verify-e2e-dual-emulator-prereqs.ps1` before e2e gates
- [x] Environment-blocked gate handling and evidence capture plan is defined — blocked gates recorded as environment blocker with missing source as unblocking step

## Project Structure

### Documentation (this feature)

```text
specs/023-per-source-frame-retention/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── per-source-frame-repository.md
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
# Android multi-module — changes span domain, data, presentation, and app wiring

feature/ndi-browser/
├── domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/
│   └── NdiRepositories.kt                        # ADD: PerSourceFrameRepository interface
├── data/src/main/java/com/ndi/feature/ndibrowser/data/repository/
│   ├── PerSourceFrameRepositoryImpl.kt            # NEW: LRU in-memory impl
│   └── NdiViewerRepositoryImpl.kt                 # MODIFY: call perSourceFrameRepo.saveFrame() on disconnect
└── presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/
    └── SourceListViewModel.kt                     # MODIFY: observe per-source map, replace single-context observer

app/src/main/java/com/ndi/app/di/
└── AppGraph.kt                                    # MODIFY: wire PerSourceFrameRepositoryImpl singleton

# Test counterparts (TDD — written FIRST)
feature/ndi-browser/
├── data/src/test/java/com/ndi/feature/ndibrowser/data/repository/
│   └── PerSourceFrameRepositoryImplTest.kt        # NEW: unit tests (LRU cap, keying, eviction)
└── presentation/src/test/java/com/ndi/feature/ndibrowser/source_list/
    └── SourceListViewModelFrameRetentionTest.kt   # NEW: unit tests (map observation, multi-source)

testing/e2e/
└── tests/                                         # NEW: per-source-frame-retention e2e spec
```

**Structure Decision**: Android Option 3 (mobile). No new Gradle modules — changes stay within existing `:feature:ndi-browser:domain`, `:feature:ndi-browser:data`, `:feature:ndi-browser:presentation`, and `:app`. `PerSourceFrameRepository` is a new domain interface + data implementation only; no new module boundary is needed for a session-scoped in-memory store of this size.

## Complexity Tracking

> No constitution violations — no entries required.
