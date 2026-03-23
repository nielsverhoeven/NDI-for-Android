# Integration Test for Prerequisite Verification with Installation
# Tests the full verification process including automatic installation

param(
    [switch]$AllowInstallation = $false  # Set to $true to actually install missing packages
)

# Source the main script
. "$PSScriptRoot\verify-android-prereqs.ps1" -SkipExecution

Describe "Integration Test" {
    It "Should have all required functions available" {
        # Test that required functions exist
        $functions = @("Test-CommandAvailable", "Add-Result", "Write-InstallationLog", "New-Prerequisite", "New-InstallationResult", "Install-AndroidPackage")
        foreach ($func in $functions) {
            if (-not (Get-Command $func -ErrorAction SilentlyContinue)) {
                throw "Function $func not found"
            }
        }
    }

    It "Should create prerequisite correctly" {
        $prereq = New-Prerequisite -Name "test-package" -Type "package"
        if ($prereq.Name -ne "test-package") {
            throw "Prerequisite creation failed"
        }
    }

    It "Should create installation result correctly" {
        $result = New-InstallationResult -PrerequisiteName "test" -Success $true
        if (-not $result.Success) {
            throw "Installation result creation failed"
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