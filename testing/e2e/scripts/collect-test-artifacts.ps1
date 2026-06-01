param(
    [Parameter(Mandatory = $true)]
    [string]$SessionId
)

<#
.SYNOPSIS
Collect and consolidate test artifacts from a dual-emulator e2e session.

.PARAMETER SessionId
The session ID that identifies the test run.

.DESCRIPTION
Gathers Playwright test reports, logs, and other artifacts from the test execution
and consolidates them in the test-results directory for analysis and CI artifact upload.
#>

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$e2eRoot = Join-Path $repoRoot 'testing\e2e'
$testResultsDir = Join-Path $repoRoot 'test-results'
$artifactsDir = Join-Path $e2eRoot 'artifacts'

Write-Output "[collect-artifacts] Starting artifact collection for session: $SessionId"
Write-Output "[collect-artifacts] Repository root: $repoRoot"
Write-Output "[collect-artifacts] Test results directory: $testResultsDir"

# Ensure test-results directory exists
if (-not (Test-Path $testResultsDir)) {
    New-Item -ItemType Directory -Path $testResultsDir -Force | Out-Null
}

# Collect Playwright test results
$playwrightResults = @()
if (Test-Path (Join-Path $e2eRoot 'artifacts')) {
    Get-ChildItem -Path (Join-Path $e2eRoot 'artifacts') -Filter '*.json' -Recurse | ForEach-Object {
        $playwrightResults += @{
            File = $_.FullName
            RelativePath = [System.IO.Path]::GetRelativePath($repoRoot, $_.FullName)
        }
    }

    if ($playwrightResults.Count -gt 0) {
        Write-Output "[collect-artifacts] Found $($playwrightResults.Count) Playwright artifact files"
        $playwrightResults | ForEach-Object {
            Write-Output "  - $($_.RelativePath)"
        }
    }
}

# Consolidate primary-status artifact
$primaryStatusPath = Join-Path $artifactsDir 'primary-status.json'
if (Test-Path $primaryStatusPath) {
    $consolidatedStatusPath = Join-Path $testResultsDir "024-dual-emulator-session-$SessionId.json"
    Copy-Item -Path $primaryStatusPath -Destination $consolidatedStatusPath -Force
    Write-Output "[collect-artifacts] Consolidated primary-status to $consolidatedStatusPath"
}

# Consolidate triage summary if present
$triagePath = Join-Path $artifactsDir 'triage-summary.json'
if (Test-Path $triagePath) {
    $consolidatedTriagePath = Join-Path $testResultsDir "024-dual-emulator-triage-$SessionId.json"
    Copy-Item -Path $triagePath -Destination $consolidatedTriagePath -Force
    Write-Output "[collect-artifacts] Consolidated triage summary to $consolidatedTriagePath"
}

# Create collection manifest
$manifest = [ordered]@{
    sessionId = $SessionId
    collectedAt = (Get-Date).ToUniversalTime().ToString('o')
    artifactFiles = $playwrightResults
    consolidationDir = [System.IO.Path]::GetRelativePath($repoRoot, $testResultsDir)
}

$manifestPath = Join-Path $testResultsDir "024-dual-emulator-manifest-$SessionId.json"
$manifest | ConvertTo-Json -Depth 5 | Set-Content -Path $manifestPath -Encoding UTF8
Write-Output "[collect-artifacts] Collection manifest written to $manifestPath"

Write-Output "[collect-artifacts] Artifact collection completed successfully"
exit 0
