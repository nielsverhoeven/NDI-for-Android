# Implementation Plan: Settings Menu End-to-End Emulator Validation

**Branch**: `008-settings-e2e-validation` | **Date**: 2026-03-20 | **Spec**: `specs/008-settings-e2e-validation/spec.md`
**Input**: Feature specification from `/specs/008-settings-e2e-validation/spec.md`

## Summary

Add and enforce end-to-end quality gates for the settings menu by defining
Playwright coverage for new/updated settings flows, running those flows on
Android emulator(s), and requiring full regression execution of existing
Playwright scenarios in the same validation cycle. The approach uses one
primary emulator profile for PR gating and scheduled matrix runs for
cross-profile compatibility confidence.

## Technical Context

**Language/Version**: TypeScript (Playwright tests), Kotlin (Android app under test), Gradle Kotlin DSL with Java toolchain 21/Java 17 bytecode target  
**Primary Dependencies**: `@playwright/test`, existing `testing/e2e` harness and scripts, Android Emulator + adb tooling, existing settings/navigation flows in `feature/ndi-browser/presentation`  
**Storage**: File-based test artifacts/reports under `testing/e2e/artifacts`, `testing/e2e/test-results`, and summary evidence in `test-results`  
**Testing**: Playwright end-to-end tests on emulator(s), existing unit/instrumentation suites remain part of release gating  
**Target Platform**: Android API 24+ app behavior validated through emulator profiles  
**Project Type**: Feature-modularized Android mobile application with Playwright e2e harness  
**Performance Goals**: PR e2e gate remains deterministic and timely while maintaining full required scenario coverage  
**Constraints**: Must run new settings scenarios plus existing Playwright regression scenarios; incomplete runs fail gate; no architecture boundary violations in app modules  
**Scale/Scope**: One e2e quality-gate feature covering settings navigation/behavior scenarios and full-suite regression policy

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
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
app/
├── src/main/java/com/ndi/app/
└── src/main/res/navigation/

feature/ndi-browser/
├── presentation/src/main/java/com/ndi/feature/ndibrowser/
├── data/src/main/java/com/ndi/feature/ndibrowser/
└── domain/src/main/java/com/ndi/feature/ndibrowser/

testing/e2e/
├── scripts/
├── tests/
└── tests/support/

test-results/
└── android-test-results.md
```

**Structure Decision**: Keep product behavior in existing Android feature modules and implement validation scope in the existing Playwright harness under `testing/e2e` with evidence recorded in `test-results`.

## Phase 0: Research Consolidation

Research output is captured in `specs/008-settings-e2e-validation/research.md`
and resolves:

- Playwright-only e2e strategy for this feature.
- PR gate strategy (single primary emulator profile).
- Scheduled matrix strategy (multi-profile compatibility coverage).
- Mandatory execution of existing Playwright regression scenarios.
- Evidence and fail-fast handling for incomplete/aborted runs.

All technical context items are resolved; no `NEEDS CLARIFICATION` entries
remain.

## Phase 1: Design & Contracts

Design artifacts are complete and mapped to the specification:

- Data model: `specs/008-settings-e2e-validation/data-model.md`
  - Defines scenarios, emulator profiles, validation runs, suite results, and quality-gate evidence.
- Contracts: `specs/008-settings-e2e-validation/contracts/settings-e2e-validation-contract.md`
  - Defines required scenario scope, PR/scheduled run obligations, pass/fail rules, and evidence contract.
- Quickstart: `specs/008-settings-e2e-validation/quickstart.md`
  - Documents prerequisite checks, PR and matrix run commands, required scenarios, and troubleshooting.
- Agent context update:
  - `.specify/scripts/bash/update-agent-context.sh copilot` executed after artifact generation.

## Constitution Check (Post-Design Re-Evaluation)

- MVVM gate: PASS - no presentation/business logic relocation is introduced.
- Navigation gate: PASS - scenarios validate existing single-activity routes.
- Repository gate: PASS - no direct UI data-access changes introduced.
- TDD gate: PASS - planning requires failing-test-first e2e additions and regression validation.
- Playwright gate: PASS - new visual-flow e2e coverage and existing-suite regression runs are mandatory.
- Material 3 gate: PASS - UI behavior under test remains Material 3-compliant existing surface.
- Battery gate: PASS - no persistent/background runtime features added.
- Offline gate: PASS - validation includes persisted settings behavior in local app context.
- Security gate: PASS - no new permissions; test artifacts avoid sensitive value exposure.
- Modularization gate: PASS - harness and app boundaries remain intact.
- Release gate: PASS - release hardening remains required and unaffected.

## Phase 2 Readiness

Planning artifacts ready for `/speckit.tasks`:

- `specs/008-settings-e2e-validation/spec.md`
- `specs/008-settings-e2e-validation/plan.md`
- `specs/008-settings-e2e-validation/research.md`
- `specs/008-settings-e2e-validation/data-model.md`
- `specs/008-settings-e2e-validation/contracts/settings-e2e-validation-contract.md`
- `specs/008-settings-e2e-validation/quickstart.md`

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No constitution violations require exception handling for this plan.
