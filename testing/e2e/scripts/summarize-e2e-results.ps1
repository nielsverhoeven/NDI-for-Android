param(
    [Parameter(Mandatory = $true)]
    [string]$Profile,

    [Parameter(Mandatory = $true)]
    [string]$NewSettingsJson,

    [Parameter(Mandatory = $true)]
    [string]$LatencyScenarioJson,

    [Parameter(Mandatory = $true)]
    [string]$ExistingRegressionJson,

    [string]$OutputPath,

    [string]$WaiverFile = $env:E2E_WAIVER_FILE
)

$ErrorActionPreference = "Stop"

function Read-PlaywrightStats {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path $Path)) {
        throw "Missing Playwright JSON report: $Path"
    }

    $json = Get-Content $Path -Raw | ConvertFrom-Json
    $stats = $json.stats
    if (-not $stats) {
        throw "Report did not include stats: $Path"
    }

    return [PSCustomObject]@{
        expected = [int]$stats.expected
        unexpected = [int]$stats.unexpected
        flaky = [int]$stats.flaky
        skipped = [int]$stats.skipped
        durationMs = [int]$stats.duration
    }
}

function Assert-WaiverMetadata {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path $Path)) {
        throw "A waiver is required but waiver file was not found: $Path"
    }

    $waiver = Get-Content $Path -Raw | ConvertFrom-Json
    $approvers = @($waiver.approvers)

    if (-not $waiver.reason) {
        throw "Waiver must include a non-empty reason."
    }

    if (-not $waiver.expiresOn) {
        throw "Waiver must include expiresOn."
    }

    $hasMobileMaintainer = $approvers | Where-Object { $_.role -eq "mobile-maintainer" }
    $hasArchReviewer = $approvers | Where-Object { $_.role -eq "architecture-quality-reviewer" }

    if (-not $hasMobileMaintainer) {
        throw "Waiver metadata missing required approver role: mobile-maintainer"
    }

    if (-not $hasArchReviewer) {
        throw "Waiver metadata missing required approver role: architecture-quality-reviewer"
    }
}

$newSettings = Read-PlaywrightStats -Path $NewSettingsJson
$latencyScenario = Read-PlaywrightStats -Path $LatencyScenarioJson
$existingRegression = Read-PlaywrightStats -Path $ExistingRegressionJson

$hasFailureOrIncomplete = (
    $newSettings.unexpected -gt 0 -or $latencyScenario.unexpected -gt 0 -or $existingRegression.unexpected -gt 0 -or
    $newSettings.skipped -gt 0 -or $latencyScenario.skipped -gt 0 -or $existingRegression.skipped -gt 0
)

$waiverUsed = $false
if ($hasFailureOrIncomplete -or (-not [string]::IsNullOrWhiteSpace($WaiverFile))) {
    Assert-WaiverMetadata -Path $WaiverFile
    $waiverUsed = $true
}

if (-not $OutputPath) {
    $OutputPath = Join-Path (Split-Path $NewSettingsJson -Parent) "summary-$Profile.md"
}

$lines = @(
    "# E2E Validation Summary",
    "",
    "- Profile: $Profile",
    "- GeneratedAtUtc: $((Get-Date).ToUniversalTime().ToString('o'))",
    "- WaiverUsed: $waiverUsed",
    "",
    "| Suite | Expected | Unexpected | Flaky | Skipped | DurationMs |",
    "|---|---:|---:|---:|---:|---:|",
    "| New Settings | $($newSettings.expected) | $($newSettings.unexpected) | $($newSettings.flaky) | $($newSettings.skipped) | $($newSettings.durationMs) |",
    "| Latency Scenario | $($latencyScenario.expected) | $($latencyScenario.unexpected) | $($latencyScenario.flaky) | $($latencyScenario.skipped) | $($latencyScenario.durationMs) |",
    "| Existing Regression | $($existingRegression.expected) | $($existingRegression.unexpected) | $($existingRegression.flaky) | $($existingRegression.skipped) | $($existingRegression.durationMs) |"
)

if ($hasFailureOrIncomplete -and -not $waiverUsed) {
    throw "Suite has failures/incomplete results and no waiver was accepted."
}

$lines -join "`n" | Out-File -FilePath $OutputPath -Encoding utf8
Write-Host "Summary written to $OutputPath"
