# Implementation Plan: Developer Log Configured IP Display

**Branch**: `028-fix-configured-ip-log` | **Date**: 2026-04-07 | **Spec**: `specs/028-fix-configured-ip-log/spec.md`
**Input**: Feature specification from `specs/028-fix-configured-ip-log/spec.md`

## Summary

Replace redacted configured-address placeholders in View screen developer logs with actual configured values (IPv4, IPv6, hostname), while preserving suppression when developer mode is off and adding fallback behavior for no valid addresses. Implementation will update the viewer logging path to resolve runtime configuration at event emission time, apply address validation/de-duplication/order preservation rules, and add Playwright plus regression coverage with preflight environment checks.

## Technical Context

**Language/Version**: Kotlin 2.2.10, Java 17 bytecode target, Gradle Kotlin DSL  
**Primary Dependencies**: AndroidX Lifecycle/ViewModel, Navigation Component, repository contracts in `feature/ndi-browser/domain`, sdk bridge/logging components in `feature/ndi-browser/presentation` and `ndi/sdk-bridge`  
**Storage**: Existing Room persistence paths remain unchanged; settings/config source remains existing runtime configuration layer  
**Testing**: JUnit unit tests, Playwright Android e2e flows under `testing/e2e`, Gradle test/install validation  
**Target Platform**: Android (minSdk 24, compile/target SDK baseline from repo), emulator + physical device validation
**Project Type**: Android multi-module mobile application  
**Performance Goals**: No perceptible regression in View screen logging/rendering behavior; log updates remain aligned with current event emission cadence  
**Constraints**: Developer-mode-only visible behavior change, no new permissions/background jobs, preserve existing navigation and telemetry patterns, maintain release hardening gates  
**Scale/Scope**: Localized to View screen developer log pipeline and associated tests/documentation for one feature slice

## Constitution Check (Pre-Design)

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] MVVM-only presentation logic enforced (no UI/business logic leakage)
- [x] Single-activity navigation compliance maintained
- [x] Repository-mediated data access preserved
- [x] TDD evidence planned (Red-Green-Refactor with failing-test-first path)
- [x] Unit test scope defined using JUnit
- [x] Playwright e2e scope defined for end-to-end flows
- [x] For visual UI additions/changes: emulator Playwright e2e tests are explicitly planned
- [x] For visual UI additions/changes: existing Playwright e2e regression run is explicitly planned
- [x] For shared persistence/settings changes: regression tests for state-preservation are explicitly planned
- [x] Material 3 compliance verification planned for UI changes (log text content only; no component redesign)
- [x] Battery/background execution impact evaluated (none expected; no new background work)
- [x] Offline-first and Room persistence constraints respected (if applicable)
- [x] Least-permission/security implications documented
- [x] Feature-module boundary compliance documented
- [x] Release hardening validation planned (R8/ProGuard + shrink resources)
- [x] Runtime preflight checks are defined for required emulators/devices/tools before quality gates
- [x] Environment-blocked gate handling and evidence capture plan is defined

## Constitution Check (Post-Design)

- [x] Design artifacts keep logic in ViewModel/repository boundaries and avoid UI-layer business logic
- [x] No navigation architecture changes introduced
- [x] Data access remains repository-mediated and settings-backed
- [x] Test-first implementation path remains mandatory in task planning
- [x] JUnit + Playwright test coverage expectations are explicit in quickstart/contract
- [x] Emulator Playwright coverage and full suite regression are explicitly required
- [x] Preflight/environment blocker process is explicit and reproducible
- [x] No constitutional violations identified; Complexity Tracking not required

## Project Structure

### Documentation (this feature)

```text
specs/028-fix-configured-ip-log/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── viewer-developer-log-contract.md
└── tasks.md
```

### Source Code (repository root)
```text
app/
├── src/main/java/com/ndi/app/
│   ├── navigation/
│   └── di/

feature/ndi-browser/
├── domain/src/main/java/com/ndi/feature/ndibrowser/domain/
├── data/src/main/java/com/ndi/feature/ndibrowser/data/
└── presentation/src/main/java/com/ndi/feature/ndibrowser/
    ├── viewer/
    └── source_list/

core/
├── database/src/main/java/com/ndi/core/database/
└── testing/

ndi/sdk-bridge/src/main/java/com/ndi/sdkbridge/

testing/e2e/
└── (Playwright Android e2e suites and scripts)
```

**Structure Decision**: Use the existing Android multi-module feature-first structure. Implementation is localized to `feature/ndi-browser/presentation` (viewer log rendering path) and related tests, while preserving existing domain/data and app navigation boundaries.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No constitutional violations identified for this feature.
