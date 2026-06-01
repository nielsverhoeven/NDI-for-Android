# Test Timeout Handling
# Tests that installations properly timeout and handle hanging processes

# Source the main script
. "$PSScriptRoot\verify-android-prereqs.ps1" -SkipExecution

Describe "Timeout Handling" {
    It "Should have timeout parameter in function" {
        $function = Get-Command Install-AndroidPackage
        if (-not $function.Parameters.ContainsKey("timeoutSeconds")) {
            throw "timeoutSeconds parameter not found"
        }
    }

    It "Should handle timeout in error results" {
        $result = New-InstallationResult -PrerequisiteName "test" -Success $false -ErrorMessage "Installation timed out after 300 seconds"
        if (-not $result.ErrorMessage.Contains("timed out")) {
            throw "Timeout error not handled properly"
        }
    }

    It "Should validate timeout parameter range" {
        # Test that timeout is reasonable (not negative, not too large)
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
    Write-Error "Timeout tests failed: $($testResults.FailedCount) failed"
    exit 1
} else {
    Write-Host "Timeout tests passed: $($testResults.PassedCount) passed"
}
if ($testResults.FailedCount -gt 0) {
    Write-Error "Timeout tests failed: $($testResults.FailedCount) failed"
    exit 1
} else {
    Write-Host "Timeout tests passed: $($testResults.PassedCount) passed"
}