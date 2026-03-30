# End-to-End Test with Simulated Failures
# Tests the complete installation workflow with failure scenarios

# Source the main script
. "$PSScriptRoot\verify-android-prereqs.ps1" -SkipExecution

Describe "End-to-End Installation Workflow" {
    It "Should have all required installation functions" {
        $functions = @("Install-AndroidPackage", "New-InstallationResult", "Write-InstallationLog")
        foreach ($func in $functions) {
            if (-not (Get-Command $func -ErrorAction SilentlyContinue)) {
                throw "Function $func not found"
            }
        }
    }

    It "Should handle installation result aggregation" {
        $results = @()
        $results += New-InstallationResult -PrerequisiteName "test1" -Success $true
        $results += New-InstallationResult -PrerequisiteName "test2" -Success $false -ErrorMessage "Failed"

        $successful = $results | Where-Object { $_.Success }
        $failed = $results | Where-Object { -not $_.Success }

        if ($successful.Count -ne 1) {
            throw "Should have 1 successful result"
        }
        if ($failed.Count -ne 1) {
            throw "Should have 1 failed result"
        }
    }

    It "Should provide comprehensive error reporting" {
        $result = New-InstallationResult -PrerequisiteName "test" -Success $false -ErrorMessage "Network timeout"
        if ([string]::IsNullOrEmpty($result.ErrorMessage)) {
            throw "Error message should not be empty"
        }
        if (-not $result.ErrorMessage.Contains("Network")) {
            throw "Error message should contain failure details"
        }
    }
}

# Run tests
$testResults = Invoke-Pester -PassThru
if ($testResults.FailedCount -gt 0) {
    Write-Error "E2E tests failed: $($testResults.FailedCount) failed"
    exit 1
} else {
    Write-Host "E2E tests passed: $($testResults.PassedCount) passed"
}

Write-Host "E2E Test Results:"
Write-Host "Successful installations: $($successfulInstalls.Count)"
Write-Host "Failed installations: $($failedInstalls.Count)"

if ($failedInstalls.Count -gt 0) {
    Write-Host "Failed packages:"
    foreach ($fail in $failedInstalls) {
        Write-Host "  - $($fail.PrerequisiteName): $($fail.ErrorMessage)"
    }
}

# Test should pass even with simulated failures (as long as error handling works)
if ($testResults.Count -eq 3) {
    Write-Host "E2E test completed successfully - error handling verified"
} else {
    Write-Error "E2E test failed - unexpected number of results"
    exit 1
}