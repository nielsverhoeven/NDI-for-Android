param(
    [string[]]$Profiles = @('pr-primary'),
    [bool]$DeveloperModeAvailable = $true,
    [string]$MatrixEvidencePath = 'test-results/029-compatibility-matrix.md'
)

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
Push-Location $repoRoot
try {
    & pwsh -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'run-matrix-e2e.ps1') -Profiles $Profiles -DeveloperModeAvailable:$DeveloperModeAvailable
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz'
    $summary = @(
        '',
        '## Matrix Execution Record',
        "- command: testing/e2e/scripts/run-discovery-compatibility-matrix.ps1",
        "- profiles: $($Profiles -join ',')",
        "- completedAt: $timestamp"
    )

    Add-Content -Path $MatrixEvidencePath -Value $summary
}
finally {
    Pop-Location
}

exit 0
