param(
    [string[]]$Profiles = @('pr-primary'),
    [bool]$DeveloperModeAvailable = $true
)

$normalizedProfiles = @()
foreach ($profile in $Profiles) {
    if ([string]::IsNullOrWhiteSpace($profile)) {
        continue
    }

    $normalizedProfiles += ($profile -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}

if ($normalizedProfiles.Count -eq 0) {
    throw 'At least one matrix profile is required.'
}

$failed = $false
foreach ($profile in $normalizedProfiles) {
    $profileOutputDir = Join-Path $PSScriptRoot "..\artifacts\matrix-$profile"
    & pwsh -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot/run-primary-pr-e2e.ps1" -Profile $profile -DeveloperModeAvailable:$DeveloperModeAvailable -OutputDir $profileOutputDir
    if ($LASTEXITCODE -ne 0) {
        $failed = $true
    }
}

if ($failed) {
    exit 1
}

exit 0
