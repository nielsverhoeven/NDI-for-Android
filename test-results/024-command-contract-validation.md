# Command Contract Validation (US4)

Executed command contract path:
- pwsh ./scripts/verify-android-prereqs.ps1 -CiMode -AllowMissingNdiSdk
- Push-Location ./testing/e2e; npm ci; Pop-Location
- pwsh ./testing/e2e/scripts/run-primary-pr-e2e.ps1 -Profile pr-primary
- Push-Location ./testing/e2e; npx playwright test tests/support/ci-artifact-contract.spec.ts tests/support/ci-workflow-contract.spec.ts; Pop-Location

Execution result:
- Android prereqs: PASS
- npm ci: PASS
- Primary profile run: PASS
- CI contract specs: PASS (10 passed)
- Exit code: 0

Primary artifact snapshot:
- status: pass
- gateDecision: pass
- missingSpecPaths: []
- artifact: testing/e2e/artifacts/primary-status.json
