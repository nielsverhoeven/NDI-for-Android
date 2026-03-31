# Implementation Plan: Rebuild Android E2E Suite

**Branch**: `[024-rebuild-android-e2e]` | **Date**: 2026-03-31 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from [specs/024-rebuild-android-e2e/spec.md](spec.md)

## Summary

Replace the existing unstable Android Playwright e2e suite with a new deterministic suite that covers settings, top-level navigation, and developer mode flows, while enforcing CI execution and artifact-based triage in GitHub Actions. The approach keeps Playwright as the e2e standard, reuses existing dual-emulator harness scripts and workflows, and introduces explicit suite classification so developer mode tests can run only on designated targets and report not-applicable elsewhere.

## Technical Context

**Language/Version**: Kotlin 2.2.10 (Android app), TypeScript (Playwright tests), PowerShell 7+ (orchestration scripts), YAML (GitHub Actions)  
**Primary Dependencies**: AndroidX Navigation + Material3 app stack, Playwright test runner (Node 20), ADB/emulator toolchain, repository e2e scripts under [testing/e2e/scripts](../../testing/e2e/scripts)  
**Storage**: N/A for feature storage changes; e2e evidence persisted as JSON/markdown artifacts under [testing/e2e/artifacts](../../testing/e2e/artifacts)  
**Testing**: Playwright e2e (primary), JUnit for non-e2e regressions when required by touched app code, CI gates in [android-ci.yml](../../.github/workflows/android-ci.yml) and [e2e-dual-emulator.yml](../../.github/workflows/e2e-dual-emulator.yml)  
**Target Platform**: Android emulator targets on Windows GitHub Actions runners and local Windows developer environments  
**Project Type**: Mobile app (multi-module Android) with Playwright-driven emulator e2e harness  
**Performance Goals**: Primary PR e2e profile completes within current CI timeout budget and produces deterministic pass/fail/not-applicable outcomes without manual intervention  
**Constraints**: Must preserve constitution requirement to default e2e to Playwright; must run preflight checks before gates; must support developer-mode-not-available targets as not-applicable instead of fail  
**Scale/Scope**: Full rebuild of e2e specs and suite manifests touching [testing/e2e/tests](../../testing/e2e/tests), [testing/e2e/scripts](../../testing/e2e/scripts), and relevant GitHub workflow gates

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] MVVM-only presentation logic enforced (no UI/business logic leakage)
- [x] Single-activity navigation compliance maintained
- [x] Repository-mediated data access preserved
- [x] TDD evidence planned (Red-Green-Refactor with failing-test-first path)
- [x] Unit test scope defined using JUnit
- [x] Playwright e2e scope defined for end-to-end flows
- [x] For visual UI additions/changes: emulator Playwright e2e tests are explicitly planned
- [x] For visual UI additions/changes: existing Playwright e2e regression run is explicitly planned (rebuilt suite becomes new regression baseline)
- [x] For shared persistence/settings changes: regression tests for state-preservation are explicitly planned where settings state is asserted in e2e
- [x] Material 3 compliance verification planned for UI changes (assertions are behavioral/visual only; no component rewrites planned)
- [x] Battery/background execution impact evaluated (no new background jobs/services introduced)
- [x] Offline-first and Room persistence constraints respected (no repository/persistence contract changes)
- [x] Least-permission/security implications documented (no new manifest permissions; test-only interactions)
- [x] Feature-module boundary compliance documented (changes isolated to e2e harness and CI/test wiring)
- [x] Release hardening validation planned (R8/ProGuard + shrink resources) as non-regression gate in implementation
- [x] Runtime preflight checks are defined for required emulators/devices/tools before quality gates
- [x] Environment-blocked gate handling and evidence capture plan is defined

## Project Structure

### Documentation (this feature)

```text
specs/024-rebuild-android-e2e/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
└── tasks.md
```

### Source Code (repository root)

```text
.github/workflows/
├── android-ci.yml
├── e2e-dual-emulator.yml
└── e2e-matrix-nightly.yml

testing/e2e/
├── playwright.config.ts
├── package.json
├── scripts/
│   ├── run-primary-pr-e2e.ps1
│   ├── run-dual-emulator-e2e.ps1
│   └── helpers/
├── tests/
│   ├── *.spec.ts
│   └── support/
│       ├── regression-suite-manifest.json
│       ├── e2e-suite-classification.spec.ts
│       └── android-ui-driver.ts
└── artifacts/

scripts/
├── verify-android-prereqs.ps1
└── verify-e2e-dual-emulator-prereqs.ps1

app/src/main/
├── java/com/ndi/app/MainActivity.kt
└── res/
    ├── navigation/main_nav_graph.xml
    └── menu/top_level_navigation_menu.xml
```

**Structure Decision**: Keep implementation in existing Android + Playwright harness structure. No new top-level modules are required; feature work is concentrated in [testing/e2e](../../testing/e2e) and workflow/preflight files with app-path references used only for test selectors/flows.

## Phase 0: Research & Decisions

Research outputs are documented in [research.md](research.md) and resolve technical choices for:

1. Rebuild strategy for retiring legacy specs while preserving deterministic gating.
2. Developer mode target policy (required on designated targets, not-applicable elsewhere).
3. CI integration strategy across primary PR gate and optional matrix/nightly coverage.
4. Evidence and blocker taxonomy for constitution-compliant reporting.

## Phase 1: Design & Contracts

Design outputs are:

1. [data-model.md](data-model.md) for suite metadata, scenario classification, run result schema, and lifecycle states.
2. [contracts/e2e-execution-contract.md](contracts/e2e-execution-contract.md) for command/workflow/reporting contracts.
3. [quickstart.md](quickstart.md) for local and CI-equivalent execution steps.

## Post-Design Constitution Re-Check

- [x] Playwright-first e2e remains explicit and mandatory for covered flows.
- [x] Preflight-first execution model is enforced in both local and CI paths.
- [x] Environment-blocked outcomes are explicitly captured with unblock actions.
- [x] No architecture-rule violations introduced (feature/test-only scope).

## Complexity Tracking

No constitution violations currently identified.
