param(
    [string]$Profiles = $env:E2E_MATRIX_PROFILES,
    [string]$OutputRoot
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Profiles)) {
    $Profiles = "api34,api35"
}

$profileList = $Profiles.Split(",") | ForEach-Object { $_.Trim() } | Where-Object { $_ }
if ($profileList.Count -eq 0) {
    throw "No matrix profiles were provided."
}

if (-not $OutputRoot) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutputRoot = Join-Path $PSScriptRoot "..\artifacts\matrix-$timestamp"
}
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null

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

function Invoke-PlaywrightJson {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Project,
        [Parameter(Mandatory = $true)]
        [string[]]$Specs,
        [Parameter(Mandatory = $true)]
        [string]$JsonPath
    )

    $playwrightArgs = @("playwright", "test", "--project=$Project", "--reporter=json") + $Specs
    & npx $playwrightArgs | Out-File -FilePath $JsonPath -Encoding utf8
    if ($LASTEXITCODE -ne 0) {
        throw "Playwright run failed for project '$Project' (exit $LASTEXITCODE)."
    }
}

Push-Location (Join-Path $PSScriptRoot "..")
try {
    $matrixRows = @()
    $failedProfiles = @()

    foreach ($profile in $profileList) {
        $projectName = "android-matrix-$profile"
        $profileDir = Join-Path $OutputRoot $profile
        New-Item -ItemType Directory -Path $profileDir -Force | Out-Null

        $newSettingsJson = Join-Path $profileDir "new-settings.json"
        $existingJson = Join-Path $profileDir "existing-regression.json"

        try {
            Invoke-PlaywrightJson -Project $projectName -Specs $newSettingsSpecs -JsonPath $newSettingsJson
            Invoke-PlaywrightJson -Project $projectName -Specs $existingRegressionSpecs -JsonPath $existingJson

            & powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "summarize-e2e-results.ps1") `
                -Profile $profile `
                -NewSettingsJson $newSettingsJson `
                -ExistingRegressionJson $existingJson `
                -OutputPath (Join-Path $profileDir "summary.md")

            if ($LASTEXITCODE -ne 0) {
                throw "Summary validation failed for profile '$profile'."
            }
        }
        catch {
            $failedProfiles += $profile
        }

        $matrixRows += [PSCustomObject]@{
            profile = $profile
            project = $projectName
            summary = (Join-Path $profileDir "summary.md")
            status = if ($failedProfiles -contains $profile) { "FAILED" } else { "PASSED" }
        }
    }

    $matrixRows | ConvertTo-Json -Depth 4 | Out-File -FilePath (Join-Path $OutputRoot "matrix-summary.json") -Encoding utf8

    if ($failedProfiles.Count -gt 0) {
        throw "Matrix run incomplete or failing for profile(s): $($failedProfiles -join ', ')"
    }

    Write-Host "Matrix run completed. Evidence: $OutputRoot"
}
finally {
    Pop-Location
}
