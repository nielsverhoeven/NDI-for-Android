param(
    [string[]]$RequiredApiLevels = @("32", "33", "34", "35"),
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"

. "$PSScriptRoot/result-handler.ps1"

try {
    $installed = (& sdkmanager --list_installed 2>&1) -join "`n"
    $missing = @()
    $found = @()

    foreach ($api in $RequiredApiLevels) {
        $pattern = "system-images;android-$api;"
        if ($installed -match [Regex]::Escape($pattern)) {
            $found += $api
        }
        else {
            $missing += $api
        }
    }

    $status = if ($missing.Count -eq 0) { "SUCCESS" } else { "FAILURE" }
    $result = New-E2eResult -Operation "validate-emulator-images" -Status $status -Data ([PSCustomObject]@{
            requiredApiLevels = $RequiredApiLevels
            foundApiLevels = $found
            missingApiLevels = $missing
        })

    if ($missing.Count -gt 0) {
        $result.errors = @(
            New-E2eError -Code "MISSING_IMAGES" -Message "Missing emulator system images." -Details @{ missingApiLevels = $missing }
        )
    }

    Exit-E2eWithResult -Result $result -OutputPath $OutputPath
}
catch {
    $result = New-E2eResult -Operation "validate-emulator-images" -Status "FAILURE" -Errors @(
        New-E2eError -Code "VALIDATION_FAILED" -Message $_.Exception.Message
    )
    Exit-E2eWithResult -Result $result -OutputPath $OutputPath
}
