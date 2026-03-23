param(
    [switch]$CiMode,
    [switch]$AllowMissingNdiSdk,
    [switch]$NoInstall,  # Skip automatic installation of missing packages
    [switch]$SkipExecution  # Skip main execution (for testing)
)

<#
.SYNOPSIS
    Verifies Android development prerequisites and optionally installs missing components.

.DESCRIPTION
    This script checks for required Android development tools and SDK components.
    In CI mode or when packages are missing, it can automatically download and install
    the required Android SDK packages using sdkmanager.

.PARAMETER CiMode
    Enables CI-specific behavior (e.g., fails fast on errors).

.PARAMETER AllowMissingNdiSdk
    Skips NDI SDK requirement check (useful for CI environments without proprietary SDK).

.PARAMETER NoInstall
    Prevents automatic installation of missing packages (verification-only mode).

.EXAMPLE
    .\verify-android-prereqs.ps1

.EXAMPLE
    .\verify-android-prereqs.ps1 -CiMode -AllowMissingNdiSdk

.NOTES
    Requires Android SDK to be installed and ANDROID_SDK_ROOT environment variable set.
    Automatic installation requires internet access and accepts SDK licenses automatically.
#>

$ErrorActionPreference = "Stop"

function Test-CommandAvailable {
    param([string]$Name)

    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

function Add-Result {
    param(
        [string]$Name,
        [bool]$Ok,
        [string]$Detail
    )

    [pscustomobject]@{
        Name = $Name
        Ok = $Ok
        Detail = $Detail
    }
}

function Write-InstallationLog {
    param(
        [string]$Message,
        [string]$Level = "INFO"
    )

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[$timestamp] [$Level] $Message"
}

function New-Prerequisite {
    param(
        [string]$Name,
        [string]$Type,
        [bool]$Required = $true,
        [string]$CheckPath = "",
        [string]$InstallCommand = ""
    )

    [pscustomobject]@{
        Name = $Name
        Type = $Type
        Required = $Required
        CheckPath = $CheckPath
        InstallCommand = $InstallCommand
    }
}

function New-InstallationResult {
    param(
        [string]$PrerequisiteName,
        [bool]$Success,
        [string]$ErrorMessage = "",
        [TimeSpan]$Duration = [TimeSpan]::Zero
    )

    [pscustomobject]@{
        PrerequisiteName = $PrerequisiteName
        Success = $Success
        ErrorMessage = $ErrorMessage
        Duration = $Duration
        Timestamp = Get-Date
    }
}

function Install-AndroidPackage {
    param(
        [string]$packageName,
        [int]$maxRetries = 3,
        [int]$timeoutSeconds = 300  # 5 minutes timeout
    )

    Write-InstallationLog "Starting installation of Android package: $packageName (timeout: ${timeoutSeconds}s)"

    $startTime = Get-Date
    $timeoutTime = $startTime.AddSeconds($timeoutSeconds)

    for ($attempt = 1; $attempt -le $maxRetries; $attempt++) {
        # Check timeout
        if ((Get-Date) -gt $timeoutTime) {
            $duration = (Get-Date) - $startTime
            return New-InstallationResult -PrerequisiteName $packageName -Success $false -ErrorMessage "Installation timed out after ${timeoutSeconds} seconds" -Duration $duration
        }

        try {
            Write-InstallationLog "Attempt $attempt/$maxRetries for package: $packageName"

            # Accept licenses first
            $licenseProcess = Start-Process -FilePath "cmd.exe" -ArgumentList "/c echo y | sdkmanager --licenses" -NoNewWindow -Wait
            if ($licenseProcess.ExitCode -ne 0) {
                throw "License acceptance failed"
            }

            # Install package
            $installProcess = Start-Process -FilePath "sdkmanager" -ArgumentList "`"$packageName`"" -NoNewWindow -Wait
            if ($installProcess.ExitCode -eq 0) {
                $duration = (Get-Date) - $startTime
                Write-InstallationLog "Successfully installed package: $packageName in $($duration.TotalSeconds) seconds"
                return New-InstallationResult -PrerequisiteName $packageName -Success $true -Duration $duration
            } else {
                throw "sdkmanager exited with code $($installProcess.ExitCode)"
            }
        }
        catch {
            $errorMessage = $_.Exception.Message
            Write-InstallationLog "Attempt $attempt failed: $errorMessage" "ERROR"

            if ($attempt -eq $maxRetries) {
                $duration = (Get-Date) - $startTime
                return New-InstallationResult -PrerequisiteName $packageName -Success $false -ErrorMessage $errorMessage -Duration $duration
            }

            # Wait before retry (exponential backoff)
            $waitSeconds = [math]::Pow(2, $attempt - 1)
            Write-InstallationLog "Waiting $waitSeconds seconds before retry..."
            Start-Sleep -Seconds $waitSeconds
        }
    }
}

$results = @()
$installationResults = @()

# First, try to set up Android SDK paths if ANDROID_SDK_ROOT is set
$androidSdkRoot = if ($env:ANDROID_SDK_ROOT) { $env:ANDROID_SDK_ROOT } elseif ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { $null }

if ($androidSdkRoot -and (Test-Path $androidSdkRoot)) {
    $pathsAdded = $false
    $pathsToAdd = @()
    
    # Candidate paths where Android tools might be located
    $candidates = @(
        (Join-Path $androidSdkRoot "platform-tools"),
        (Join-Path $androidSdkRoot "cmdline-tools\latest\bin"),
        (Join-Path $androidSdkRoot "cmdline-tools\bin"),
        (Join-Path $androidSdkRoot "tools\bin")
    )
    
    # Add valid paths to PATH
    foreach ($candidatePath in $candidates) {
        if (Test-Path $candidatePath) {
            # Normalize path
            $candidatePath = (Get-Item $candidatePath).FullName
            
            # Check if already in PATH
            if (-not ($env:PATH -split ";" | Where-Object { $_ -eq $candidatePath })) {
                $env:PATH = "$candidatePath;$env:PATH"
                $pathsAdded = $true
            }
        }
    }
    
    # In CI mode, persist PATH changes to GitHub Actions environment
    if ($pathsAdded -and $CiMode -and $env:GITHUB_ENV) {
        Write-InstallationLog "Writing updated PATH to GITHUB_ENV for subsequent steps"
        "PATH=$env:PATH" | Out-File -FilePath $env:GITHUB_ENV -Encoding utf8 -Append
    }
}

$requiredCommands = @("java", "javac", "adb", "sdkmanager", "avdmanager", "emulator", "cmake", "ninja")
foreach ($command in $requiredCommands) {
    $available = Test-CommandAvailable -Name $command
    $detail = if ($available) { "Found on PATH" } else { "Missing from PATH" }
    $results += Add-Result -Name "command:$command" -Ok $available -Detail $detail
}

$gradleWrapperPath = Join-Path $PSScriptRoot "..\gradlew.bat"
$gradleAvailable = (Test-Path $gradleWrapperPath) -or (Test-CommandAvailable -Name "gradle")
$gradleDetail = if (Test-Path $gradleWrapperPath) {
    $gradleWrapperPath
} elseif (Test-CommandAvailable -Name "gradle") {
    "Global gradle available on PATH"
} else {
    "Missing Gradle wrapper and no global gradle on PATH"
}
$results += Add-Result -Name "build:gradle" -Ok $gradleAvailable -Detail $gradleDetail

$javaHome = $env:JAVA_HOME

$results += Add-Result -Name "env:JAVA_HOME" -Ok (-not [string]::IsNullOrWhiteSpace($javaHome)) -Detail ($(if ($javaHome) { $javaHome } else { "Unset" }))
$results += Add-Result -Name "env:ANDROID_SDK_ROOT" -Ok (-not [string]::IsNullOrWhiteSpace($androidSdkRoot)) -Detail ($(if ($androidSdkRoot) { $androidSdkRoot } else { "Unset" }))

$ndiSdkCandidates = @()
if (-not [string]::IsNullOrWhiteSpace($env:NDI_SDK_DIR)) {
    $ndiSdkCandidates += $env:NDI_SDK_DIR
}

$repoLocalProperties = Join-Path $PSScriptRoot "..\local.properties"
if (Test-Path $repoLocalProperties) {
    $ndiLine = Select-String -Path $repoLocalProperties -Pattern "^ndi\.sdk\.dir=(.+)$" -ErrorAction SilentlyContinue
    if ($ndiLine) {
        $ndiSdkCandidates += (($ndiLine.Matches[0].Groups[1].Value) -replace "\\\\", "\\" -replace "C\\:", "C:")
    }
}

$ndiSdkCandidates += "C:\Program Files\NDI\NDI 6 SDK (Android)"
$ndiSdkPath = $ndiSdkCandidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

$ndiOk = $AllowMissingNdiSdk.IsPresent -or (-not [string]::IsNullOrWhiteSpace($ndiSdkPath))
$ndiDetail = if ($ndiSdkPath) { $ndiSdkPath } elseif ($AllowMissingNdiSdk) { "Skipped in current mode" } else { "NDI Android SDK not found" }
$results += Add-Result -Name "sdk:NDI" -Ok $ndiOk -Detail $ndiDetail

$androidPackages = @("platform-tools", "platforms;android-34", "build-tools;34.0.0")
if ($androidSdkRoot) {
    foreach ($package in $androidPackages) {
        $packagePath = Join-Path $androidSdkRoot ($package -replace ";", "\\")
        $exists = Test-Path $packagePath

        if (-not $exists) {
            Write-InstallationLog "Package $package missing, attempting installation"
            $installResult = Install-AndroidPackage -packageName $package
            $installationResults += $installResult
            $exists = $installResult.Success
        }

        $results += Add-Result -Name "package:$package" -Ok $exists -Detail $packagePath
    }
}

$failed = $results | Where-Object { -not $_.Ok }

if (-not $SkipExecution) {
    Write-Host "Android prerequisite verification"
    foreach ($result in $results) {
        $status = if ($result.Ok) { "PASS" } else { "FAIL" }
        Write-Host ("[{0}] {1} - {2}" -f $status, $result.Name, $result.Detail)
    }

    if ($installationResults.Count -gt 0) {
        Write-Host ""
        Write-Host "Installation Summary:"
        foreach ($install in $installationResults) {
            $status = if ($install.Success) { "SUCCESS" } else { "FAILED" }
            Write-Host ("[{0}] {1} - {2} ({3}s)" -f $status, $install.PrerequisiteName, $(if ($install.Success) { "Installed" } else { $install.ErrorMessage }), $install.Duration.TotalSeconds)
        }
    }

    if ($failed.Count -gt 0) {
        if ($CiMode) {
            Write-Error ("Prerequisite verification failed for {0} checks." -f $failed.Count)
        } else {
            exit 1
        }
    }
}
