# Implementation Plan: Rebuild Android E2E Suite

**Branch**: `[024-rebuild-android-e2e]` | **Date**: 2026-03-31 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from [specs/024-rebuild-android-e2e/spec.md](spec.md)

## Summary

Rebuild the Android Playwright e2e suite from a clean baseline after retiring legacy tests, then enforce deterministic CI execution for settings, navigation, and developer-mode flows with explicit pass/fail/blocked/not-applicable taxonomy, artifact-backed triage, and reliability tracking over a rolling 20-run window.

## Technical Context

**Language/Version**: Kotlin 2.2.10 (Android app), TypeScript (Playwright tests), PowerShell 7+ (orchestration), YAML (GitHub Actions)  
**Primary Dependencies**: AndroidX Navigation + Material3 app stack, Playwright test runner on Node 20, Android SDK emulator/ADB toolchain, repository preflight scripts in [scripts](../../scripts), Playwright agent workflows (planner/generator/healer)  
**Storage**: N/A for product storage changes; e2e evidence artifacts in [testing/e2e/artifacts](../../testing/e2e/artifacts) and [test-results](../../test-results)  
**Testing**: Playwright e2e as primary end-to-end framework, JUnit only if app code changes require regression checks  
**Target Platform**: Windows local development and Windows GitHub Actions runners with Android emulator targets  
**Project Type**: Multi-module Android app with external Playwright e2e harness  
**Performance Goals**: Required PR-gate profiles meet at least 95% nondeterministic-failure-free completion over latest 20 unchanged-code runs; failed-run triage classification in 15 minutes or less  
**Constraints**: Preflight-first execution is mandatory; required profile gating fails on fail/blocked only; developer-mode unsupported targets report not-applicable; deterministic waits/retries required for timing-sensitive assertions  
**Scale/Scope**: Rebuild e2e harness assets and CI contracts across [testing/e2e](../../testing/e2e), workflow definitions under [.github/workflows](../../.github/workflows), and feature documentation under [specs/024-rebuild-android-e2e](.)

## Constitution Check

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
- [x] Material 3 compliance verification planned for UI changes
- [x] Battery/background execution impact evaluated
- [x] Offline-first and Room persistence constraints respected (if applicable)
- [x] Least-permission/security implications documented
- [x] Feature-module boundary compliance documented
- [x] Release hardening validation planned (R8/ProGuard + shrink resources)
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
│   └── e2e-execution-contract.md
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
│   ├── run-matrix-e2e.ps1
│   └── helpers/
├── tests/
│   ├── *.spec.ts
│   └── support/
│       ├── regression-suite-manifest.json
│       ├── e2e-suite-classification.spec.ts
│       ├── ci-artifact-contract.spec.ts
│       ├── ci-workflow-contract.spec.ts
│       └── android-ui-driver.ts
└── artifacts/

scripts/
├── verify-android-prereqs.ps1
└── verify-e2e-dual-emulator-prereqs.ps1
```

**Structure Decision**: Keep feature implementation in the existing Android + Playwright e2e harness structure. Work is intentionally scoped to test, script, and workflow layers with no new Gradle modules.

## Phase 0: Research & Decisions

Research output documented in [research.md](research.md) resolves:

1. Canonical status taxonomy and gating behavior (pass/fail/blocked/not-applicable).
2. Reliability measurement method for rolling 20-run window and 95% threshold.
3. Triage artifact schema and 15-minute classification evidence model.
4. Deterministic edge-case policy for install failure, timing drift, feature-flag gating, and missing setup data.
5. Playwright planner/generator/healer agent workflow obligations for this feature.

## Phase 1: Design & Contracts

Design artifacts generated:

1. [data-model.md](data-model.md) with entities for scenario manifests, execution targets, run results, and triage evidence.
2. [contracts/e2e-execution-contract.md](contracts/e2e-execution-contract.md) with command, workflow, status, gating, reliability, and triage contracts.
3. [quickstart.md](quickstart.md) with validated local/CI command path and agent-assisted workflow steps.

## Post-Design Constitution Re-Check

- [x] Playwright-first e2e remains explicit and mandatory for covered flows.
- [x] Red-Green-Refactor workflow remains required in tasks sequencing.
- [x] Runtime preflight checks are mandatory before execution gates.
- [x] Blocked versus product/test failure taxonomy is explicit and artifact-backed.
- [x] No architecture-boundary violations introduced (test and workflow scope only).

## Complexity Tracking

No constitution violations identified.
