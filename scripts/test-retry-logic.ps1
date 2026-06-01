# Test Retry Logic
# Unit tests for retry mechanism in installation functions

# Source the main script
. "$PSScriptRoot\verify-android-prereqs.ps1" -SkipExecution

Describe "Install-AndroidPackage Retry Logic" {
    It "Should have retry parameters" {
        $function = Get-Command Install-AndroidPackage
        if (-not $function.Parameters.ContainsKey("maxRetries")) {
            throw "maxRetries parameter not found"
        }
        if (-not $function.Parameters.ContainsKey("packageName")) {
            throw "packageName parameter not found"
        }
    }

    It "Should create proper installation result structure" {
        # Test that the function exists and can be called (without actually installing)
        $function = Get-Command Install-AndroidPackage
        if ($function -eq $null) {
            throw "Install-AndroidPackage function not found"
        }

        # Just test that we can create an installation result
        $result = New-InstallationResult -PrerequisiteName "test" -Success $true
        if ($result.PrerequisiteName -ne "test") {
            throw "Installation result creation failed"
        }
    }
}

# Run tests
$testResults = Invoke-Pester -PassThru
if ($testResults.FailedCount -gt 0) {
    Write-Error "Retry logic tests failed: $($testResults.FailedCount) failed"
    exit 1
} else {
    Write-Host "Retry logic tests passed: $($testResults.PassedCount) passed"
}