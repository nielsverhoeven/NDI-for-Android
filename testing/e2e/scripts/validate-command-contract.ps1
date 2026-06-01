param(
    [switch]$Execute
)

$commands = @(
    'pwsh ./scripts/verify-android-prereqs.ps1 -CiMode -AllowMissingNdiSdk',
    'Push-Location ./testing/e2e; npm ci; Pop-Location',
    'Push-Location ./testing/e2e; npx playwright test tests/support/ci-artifact-contract.spec.ts tests/support/ci-workflow-contract.spec.ts; Pop-Location',
    'pwsh ./testing/e2e/scripts/run-primary-pr-e2e.ps1 -Profile pr-primary'
)

Write-Output 'Validated command contract paths:'
$commands | ForEach-Object { Write-Output "- $_" }

if (-not $Execute) {
    Write-Output 'Dry-run only. Use -Execute to run commands.'
    exit 0
}

pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-android-prereqs.ps1 -CiMode -AllowMissingNdiSdk
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Push-Location ./testing/e2e
npm ci
$npmCode = $LASTEXITCODE
Pop-Location
if ($npmCode -ne 0) { exit $npmCode }

pwsh -NoProfile -ExecutionPolicy Bypass -File ./testing/e2e/scripts/run-primary-pr-e2e.ps1 -Profile pr-primary
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Push-Location ./testing/e2e
npx playwright test tests/support/ci-artifact-contract.spec.ts tests/support/ci-workflow-contract.spec.ts
$testCode = $LASTEXITCODE
Pop-Location
exit $testCode
