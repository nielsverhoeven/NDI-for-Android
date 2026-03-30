# Test Install Functions
# Unit tests for installation functions in verify-android-prereqs.ps1

# Source the main script to get functions
. "$PSScriptRoot\verify-android-prereqs.ps1" -SkipExecution

Describe "Install-AndroidPackage" {
    It "Should have Install-AndroidPackage function defined" {
        $function = Get-Command Install-AndroidPackage -ErrorAction SilentlyContinue
        if ($function -eq $null) {
            throw "Install-AndroidPackage function not found"
        }
    }

    It "Should have correct parameters" {
        $function = Get-Command Install-AndroidPackage
        if (-not $function.Parameters.ContainsKey("packageName")) {
            throw "packageName parameter not found"
        }
        if (-not $function.Parameters.ContainsKey("maxRetries")) {
            throw "maxRetries parameter not found"
        }
    }
}

Describe "New-InstallationResult" {
    It "Should create installation result object" {
        $result = New-InstallationResult -PrerequisiteName "test" -Success $true
        if ($result.PrerequisiteName -ne "test") {
            throw "PrerequisiteName not set correctly"
        }
        if ($result.Success -ne $true) {
            throw "Success not set correctly"
        }
        if ($result.Timestamp -eq $null) {
            throw "Timestamp not set"
        }
    }
}

# Run tests
$testResults = Invoke-Pester -PassThru
if ($testResults.FailedCount -gt 0) {
    Write-Error "Tests failed: $($testResults.FailedCount) failed out of $($testResults.TotalCount) total"
    exit 1
} else {
    Write-Host "All tests passed: $($testResults.PassedCount) passed"
}