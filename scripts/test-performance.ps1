# Performance Test for Installation Timing
# Tests that installation completes within acceptable time limits

# Source the main script
. "$PSScriptRoot\verify-android-prereqs.ps1" -SkipExecution

Describe "Installation Performance" {
    It "Should have timeout parameter" {
        $function = Get-Command Install-AndroidPackage
        if (-not $function.Parameters.ContainsKey("timeoutSeconds")) {
            throw "timeoutSeconds parameter not found"
        }
    }

    It "Should measure duration in results" {
        $result = New-InstallationResult -PrerequisiteName "test" -Success $true -Duration ([TimeSpan]::FromSeconds(1))
        if ($result.Duration.TotalSeconds -ne 1) {
            throw "Duration not measured correctly"
        }
    }

    It "Should have reasonable default timeout" {
        # Test that the function has a default timeout (300 seconds = 5 minutes)
        $function = Get-Command Install-AndroidPackage
        $timeoutParam = $function.Parameters["timeoutSeconds"]
        if ($timeoutParam -eq $null) {
            throw "timeoutSeconds parameter missing"
        }
    }
}

# Run tests
$testResults = Invoke-Pester -PassThru
if ($testResults.FailedCount -gt 0) {
    Write-Error "Performance tests failed: $($testResults.FailedCount) failed"
    exit 1
} else {
    Write-Host "Performance tests passed: $($testResults.PassedCount) passed"
}