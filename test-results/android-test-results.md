# Android Test Results

## Scope
- Branch: `024-rebuild-android-e2e`
- Commit: `fb8227ec5a30e0bd6baa4341a70f9ff6105da2d3`
- Validation target: final focused validation of feature 024 Playwright scripts, rebuilt suites, matrix wrapper, and evidence artifacts currently in the workspace.
- Changed areas validated: `testing/e2e/scripts/run-primary-pr-e2e.ps1`, `testing/e2e/scripts/run-matrix-e2e.ps1`, `testing/e2e/scripts/validate-command-contract.ps1`, `testing/e2e/tests/**/*.spec.ts`, and `test-results/024-*.md`.
- Related spec/task set: `specs/024-rebuild-android-e2e/tasks.md`, especially T016, T038-T040, T054, T063, T072, and T073.

## Stage Results
| Stage | Command(s) | Result | Notes |
|---|---|---|---|
| Prerequisite gate | `./scripts/verify-android-prereqs.ps1`; `./gradlew.bat --version`; `./scripts/verify-e2e-dual-emulator-prereqs.ps1` | PASS | Android and dual-emulator preflight checks passed on this machine. |
| Rebuilt scenario specs | `Push-Location ./testing/e2e; npx playwright test tests/024-core-settings-smoke.spec.ts tests/024-core-navigation-smoke.spec.ts tests/024-settings-menu-rebuild.spec.ts tests/024-navigation-menu-rebuild.spec.ts tests/024-developer-mode-rebuild.spec.ts; Pop-Location` | PASS | 5 rebuilt baseline scenarios passed. |
| Support + contract specs | `Push-Location ./testing/e2e; npx playwright test tests/support/e2e-suite-classification.spec.ts tests/support/regression-suite-integrity.spec.ts tests/support/ci-artifact-contract.spec.ts tests/support/ci-workflow-contract.spec.ts; Pop-Location` | PASS | 18 support and artifact/workflow contract tests passed against refreshed artifacts. |
| Command-contract execute path | `pwsh -NoProfile -ExecutionPolicy Bypass -File ./testing/e2e/scripts/validate-command-contract.ps1 -Execute` | PASS | Prereqs, `npm ci`, primary profile execution, and CI contract specs completed successfully. |
| Matrix wrapper | `Push-Location ./testing/e2e; npm run test:matrix; Pop-Location` | PASS | Executed `pr-primary`, `us2-only`, and `us3-only` profiles independently: 5 + 2 + 1 tests passed. |
| Evidence artifact sweep | `test-results/024-*.md`; `testing/e2e/artifacts/*.json`; `testing/e2e/artifacts/matrix-*/primary-status.json` | PASS WITH RISKS | Required 024 evidence files are present and current runtime artifacts now show `status=pass` with no missing spec paths. |

## Issues Found & Fixes
| Defect | Root cause | Fix status | Verification |
|---|---|---|---|
| Matrix wrapper treated all profiles as one value and overwrote the shared status artifact with invalid data. | `npm run test:matrix` passed a comma-separated profile string that `run-matrix-e2e.ps1` did not normalize, and every profile wrote to the same output path. | Fixed in `testing/e2e/scripts/run-matrix-e2e.ps1`. | Matrix run now creates `matrix-pr-primary`, `matrix-us2-only`, and `matrix-us3-only` artifacts, all with `status=pass`. |
| Primary runner reported matrix profiles as blocked. | `run-primary-pr-e2e.ps1` resolved manifest spec paths against the wrong base path when called from nested e2e contexts. | Fixed in `testing/e2e/scripts/run-primary-pr-e2e.ps1`. | Matrix artifact `missingSpecPaths` is now empty for all three profiles. |
| Primary runner could not invoke Playwright dynamically. | The script passed spec arguments in a form that Playwright did not match, and generated Windows path separators in the runtime argument list. | Fixed in `testing/e2e/scripts/run-primary-pr-e2e.ps1`. | `validate-command-contract.ps1 -Execute` and `npm run test:matrix` now pass end to end. |
| Green runs could leave stale triage output behind. | `triage-summary.json` was only written on failures and never removed on recovery. | Fixed in `testing/e2e/scripts/run-primary-pr-e2e.ps1`. | Green reruns now leave only `primary-status.json` in the shared artifact root. |

## E2E Evidence
- Executed locally:
  - `./scripts/verify-android-prereqs.ps1`
  - `./gradlew.bat --version`
  - `./scripts/verify-e2e-dual-emulator-prereqs.ps1`
  - `pwsh -NoProfile -ExecutionPolicy Bypass -File ./testing/e2e/scripts/validate-command-contract.ps1 -Execute`
  - `Push-Location ./testing/e2e; npx playwright test tests/support/e2e-suite-classification.spec.ts tests/support/regression-suite-integrity.spec.ts tests/support/ci-artifact-contract.spec.ts tests/support/ci-workflow-contract.spec.ts; Pop-Location`
  - `Push-Location ./testing/e2e; npx playwright test tests/024-core-settings-smoke.spec.ts tests/024-core-navigation-smoke.spec.ts tests/024-settings-menu-rebuild.spec.ts tests/024-navigation-menu-rebuild.spec.ts tests/024-developer-mode-rebuild.spec.ts; Pop-Location`
  - `Push-Location ./testing/e2e; npm run test:matrix; Pop-Location`
- Artifact observations:
  - `testing/e2e/artifacts/primary-status.json` reports `status=pass`, `profile=pr-primary`, `gateDecision=pass`, and `missingSpecPaths=[]`.
  - `testing/e2e/artifacts/matrix-pr-primary/primary-status.json`, `testing/e2e/artifacts/matrix-us2-only/primary-status.json`, and `testing/e2e/artifacts/matrix-us3-only/primary-status.json` all report `status=pass`.
  - Shared `testing/e2e/artifacts/triage-summary.json` is no longer present after green reruns.
- Dual-emulator run status: preflight only in this validation pass; full dual-emulator harness was not rerun.

## Release Gate Status
- [x] Android prerequisite gate passed.
- [x] Dual-emulator preflight passed.
- [x] Rebuilt Playwright scenario specs passed.
- [x] Support and CI contract specs passed.
- [x] `validate-command-contract.ps1 -Execute` passed.
- [x] `npm run test:matrix` passed.
- [x] Shared and matrix status artifacts report canonical pass outcomes with no missing spec paths.
- [ ] Full tester-mode Android assemble/unit/instrumentation/e2e stages were not rerun because this validation was scoped to feature 024 e2e/workflow assets.
- Final disposition: PASS for the requested feature 024 final validation scope, with residual evidence-quality and real-device coverage risks noted below.