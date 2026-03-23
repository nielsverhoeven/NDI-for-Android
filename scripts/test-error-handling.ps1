# Test Error Handling
# Tests for error handling in installation functions

# Source the main script
. "$PSScriptRoot\verify-android-prereqs.ps1" -SkipExecution

Describe "Error Handling in Install-AndroidPackage" {
    It "Should have error handling parameters" {
        $function = Get-Command Install-AndroidPackage
        if (-not $function.Parameters.ContainsKey("packageName")) {
            throw "packageName parameter not found"
        }
    }

    It "Should create error results properly" {
        $result = New-InstallationResult -PrerequisiteName "test" -Success $false -ErrorMessage "Test error"
        if ($result.Success -ne $false) {
            throw "Success should be false"
        }
        if ($result.ErrorMessage -ne "Test error") {
            throw "Error message not set correctly"
        }
    }
}

Describe "Logging Functions" {
    It "Should have logging function available" {
        $function = Get-Command Write-InstallationLog -ErrorAction SilentlyContinue
        if ($function -eq $null) {
            throw "Write-InstallationLog function not found"
        }
    }
}

# Run tests
$testResults = Invoke-Pester -PassThru
if ($testResults.FailedCount -gt 0) {
    Write-Error "Error handling tests failed: $($testResults.FailedCount) failed"
    exit 1
} else {
    Write-Host "Error handling tests passed: $($testResults.PassedCount) passed"
}