param(
    [string]$Status = 'auto',
    [bool]$RequiredProfile = $true,
    [string]$Profile = 'pr-primary',
    [bool]$DeveloperModeAvailable = $true,
    [switch]$ValidateCommandContract,
    [string]$OutputDir = "$(Join-Path $PSScriptRoot '..\artifacts')"
)

. "$PSScriptRoot\helpers\result-handler.ps1"
. "$PSScriptRoot\helpers\triage-summary.ps1"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$e2eRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$manifestPath = Join-Path $PSScriptRoot '..\tests\support\regression-suite-manifest.json'
$manifest = Get-Content -Path $manifestPath -Raw | ConvertFrom-Json
$selectedScenarioIds = $manifest.profiles.$Profile
$selectedSpecs = @()
$missingSpecs = @()
$notApplicableScenarioIds = @()

foreach ($scenarioId in $selectedScenarioIds) {
    $scenario = $manifest.scenarios | Where-Object { $_.id -eq $scenarioId } | Select-Object -First 1
    if ($null -ne $scenario) {
        if (($scenario.PSObject.Properties.Name -contains 'requiresDeveloperMode') -and $scenario.requiresDeveloperMode -and -not $DeveloperModeAvailable) {
            $notApplicableScenarioIds += $scenario.id
            continue
        }

        $resolvedSpecPath = Join-Path $repoRoot $scenario.specPath

        if (Test-Path $resolvedSpecPath) {
            $relativeSpecPath = [System.IO.Path]::GetRelativePath($e2eRoot, $resolvedSpecPath).Replace([System.IO.Path]::DirectorySeparatorChar, '/')
            $selectedSpecs += $relativeSpecPath
        }
        else {
            $missingSpecs += $scenario.specPath
        }
    }
}

if ($ValidateCommandContract) {
    & pwsh -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'validate-command-contract.ps1')
    if ($LASTEXITCODE -ne 0) {
        $Status = 'blocked'
    }
}

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$normalized = $Status

if ($normalized -eq 'auto') {
    if ($missingSpecs.Count -gt 0) {
        $normalized = 'blocked'
    }
    elseif ($selectedSpecs.Count -eq 0 -and $notApplicableScenarioIds.Count -gt 0) {
        $normalized = 'not-applicable'
    }
    else {
        Push-Location (Join-Path $PSScriptRoot '..')
        & npx playwright test $selectedSpecs
        $playwrightExitCode = $LASTEXITCODE
        Pop-Location

        if ($playwrightExitCode -eq 0) {
            $normalized = 'pass'
        }
        else {
            $normalized = 'fail'
        }
    }
}

$normalized = Normalize-E2eStatus -Status $normalized
$gate = Get-GateDecision -Status $normalized -RequiredProfile $RequiredProfile

$result = [ordered]@{
    status = $normalized
    requiredProfile = $RequiredProfile
    profile = $Profile
    activeSuiteId = $manifest.baseline.activeSuiteId
    selectedScenarioIds = $selectedScenarioIds
    notApplicableScenarioIds = $notApplicableScenarioIds
    missingSpecPaths = $missingSpecs
    gateDecision = $gate
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
}

$resultPath = Join-Path $OutputDir 'primary-status.json'
$result | ConvertTo-Json -Depth 5 | Set-Content -Path $resultPath -Encoding UTF8

$triagePath = Join-Path $OutputDir 'triage-summary.json'
if ($normalized -eq 'fail' -or $normalized -eq 'blocked') {
    $rootCause = if ($normalized -eq 'blocked') { 'environment-blocker' } else { 'test-defect' }
    New-TriageSummary -Status $normalized -ScenarioIds $selectedScenarioIds -RootCauseCategory $rootCause -OutputPath $triagePath | Out-Null
}
elseif (Test-Path $triagePath) {
    Remove-Item -Path $triagePath -Force
}

if ($gate -eq 'fail') {
    exit 1
}

exit 0
