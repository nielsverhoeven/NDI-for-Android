param(
    [string]$PrimaryProject = "android-primary",
    [string]$OutputRoot
)

$ErrorActionPreference = "Stop"

if (-not $OutputRoot) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutputRoot = Join-Path $PSScriptRoot "..\artifacts\primary-pr-$timestamp"
}

New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null

$resultsDir = Join-Path $OutputRoot "results"
New-Item -ItemType Directory -Path $resultsDir -Force | Out-Null

$newSettingsSpecs = @(
    "tests/settings-navigation-source-list.spec.ts",
    "tests/settings-navigation-viewer.spec.ts",
    "tests/settings-navigation-output.spec.ts",
    "tests/settings-valid-discovery-persistence.spec.ts",
    "tests/settings-invalid-discovery-validation.spec.ts",
    "tests/settings-discovery-fallback.spec.ts",
    "tests/settings-discovery-config.spec.ts"
)

$manifestPath = Join-Path $PSScriptRoot "..\tests\support\regression-suite-manifest.json"
if (-not (Test-Path $manifestPath)) {
    throw "Missing regression suite manifest: $manifestPath"
}

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$existingRegressionSpecs = @($manifest.specs)
if ($existingRegressionSpecs.Count -eq 0) {
    throw "Regression suite manifest did not include any spec entries."
}

function Invoke-PlaywrightSuite {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [string[]]$Specs,
        [Parameter(Mandatory = $true)]
        [string]$JsonPath,
        [string]$Grep
    )

    Write-Host "Running suite '$Name' on project '$PrimaryProject'"
    Write-Host "Specs: $($Specs -join ', ')"
    if ($Grep) {
        Write-Host "Grep filter: $Grep"
    }
    Write-Host "Output directory: $resultsDir/$Name"
    Write-Host "JSON capture path: $JsonPath"

    $playwrightArgs = @("playwright", "test", "--project=$PrimaryProject", "--workers=1", "--reporter=line,json", "--output", "$resultsDir/$Name")
    if ($Grep) {
        $playwrightArgs += @("--grep", $Grep)
    }
    $playwrightArgs += $Specs

    Write-Host "Command: npx $($playwrightArgs -join ' ')"
    Write-Host "--- Playwright Output Start ---"

    # Execute and stream logs while capturing structured JSON to file
    $playwrightRaw = & npx @playwrightArgs 2>&1 | Tee-Object -FilePath $JsonPath
    $playwrightExitCode = $LASTEXITCODE

    Write-Host "--- Playwright Output End ---"
    Write-Host "Exit code: $playwrightExitCode"

    if ($playwrightExitCode -ne 0) {
        Write-Host "Playwright failed with exit code $playwrightExitCode" -ForegroundColor Red
        if (Test-Path $JsonPath) {
            Write-Host "Captured result snippet (first 2000 chars):"
            Write-Host ($playwrightRaw -join "`n").Substring(0, [Math]::Min(2000, ($playwrightRaw -join "`n").Length))
        }

        throw "Playwright suite '$Name' failed with exit code $playwrightExitCode"
    }

    if (-not (Test-Path $JsonPath)) {
        throw "Suite '$Name' did not emit JSON report at $JsonPath"
    }

    Write-Host "Suite '$Name' finished successfully" -ForegroundColor Green
}

Push-Location (Join-Path $PSScriptRoot "..")
try {
    Write-Host "Ensuring Playwright browsers are installed for e2e execution"
    npx playwright install --with-deps

    $newSettingsJson = Join-Path $OutputRoot "new-settings.json"
    $latencyJson = Join-Path $OutputRoot "latency-scenario.json"
    $existingJson = Join-Path $OutputRoot "existing-regression.json"

    Invoke-PlaywrightSuite -Name "new-settings" -Specs $newSettingsSpecs -JsonPath $newSettingsJson
    Invoke-PlaywrightSuite -Name "latency-scenario" -Specs @(
        "tests/support/latency-analysis.spec.ts",
        "tests/support/scenario-checkpoints.spec.ts"
    ) -JsonPath $latencyJson -Grep "@latency"
    Invoke-PlaywrightSuite -Name "existing-regression" -Specs $existingRegressionSpecs -JsonPath $existingJson

    & powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "summarize-e2e-results.ps1") `
        -Profile "primary" `
        -NewSettingsJson $newSettingsJson `
        -LatencyScenarioJson $latencyJson `
        -ExistingRegressionJson $existingJson `
        -OutputPath (Join-Path $OutputRoot "summary-primary.md")

    if ($LASTEXITCODE -ne 0) {
        throw "Summary script failed with exit code $LASTEXITCODE"
    }

    Write-Host "Primary PR gate completed. Evidence: $OutputRoot"
}
finally {
    Pop-Location
}
