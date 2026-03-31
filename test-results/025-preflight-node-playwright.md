Validated command contract paths:
- pwsh ./scripts/verify-android-prereqs.ps1 -CiMode -AllowMissingNdiSdk
- Push-Location ./testing/e2e; npm ci; Pop-Location
- Push-Location ./testing/e2e; npx playwright test tests/support/ci-artifact-contract.spec.ts tests/support/ci-workflow-contract.spec.ts; Pop-Location
- pwsh ./testing/e2e/scripts/run-primary-pr-e2e.ps1 -Profile pr-primary
Dry-run only. Use -Execute to run commands.
