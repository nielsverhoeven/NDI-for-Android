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
        [string]$JsonPath
    )

    Write-Host "Running suite '$Name' on project '$PrimaryProject'"
    $playwrightArgs = @("playwright", "test", "--project=$PrimaryProject", "--reporter=json", "--output", "$resultsDir/$Name") + $Specs
    & npx $playwrightArgs | Out-File -FilePath $JsonPath -Encoding utf8
    if ($LASTEXITCODE -ne 0) {
        throw "Playwright suite '$Name' failed with exit code $LASTEXITCODE"
    }

    if (-not (Test-Path $JsonPath)) {
        throw "Suite '$Name' did not emit JSON report at $JsonPath"
    }
}

Push-Location (Join-Path $PSScriptRoot "..")
try {
    $newSettingsJson = Join-Path $OutputRoot "new-settings.json"
    $existingJson = Join-Path $OutputRoot "existing-regression.json"

    Invoke-PlaywrightSuite -Name "new-settings" -Specs $newSettingsSpecs -JsonPath $newSettingsJson
    Invoke-PlaywrightSuite -Name "existing-regression" -Specs $existingRegressionSpecs -JsonPath $existingJson

    & powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "summarize-e2e-results.ps1") `
        -Profile "primary" `
        -NewSettingsJson $newSettingsJson `
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
