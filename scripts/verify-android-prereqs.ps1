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

function Test-WingetAvailable {
    return [bool](Get-Command winget -ErrorAction SilentlyContinue)
}

function Add-PathEntry {
    param([string]$PathEntry)

    if ([string]::IsNullOrWhiteSpace($PathEntry) -or -not (Test-Path $PathEntry)) {
        return
    }

    $normalized = (Get-Item $PathEntry).FullName
    $existingEntries = $env:PATH -split ";"
    if (-not ($existingEntries | Where-Object { $_ -eq $normalized })) {
        $env:PATH = "$normalized;$env:PATH"
    }
}

function Update-ProcessPathFromSystem {
    $machinePath = [Environment]::GetEnvironmentVariable("Path", "Machine")
    $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
    $mergedPath = @($machinePath, $userPath) -join ";"
    if (-not [string]::IsNullOrWhiteSpace($mergedPath)) {
        $env:PATH = $mergedPath
    }

    # Winget shims and package folders can be present before a new shell is opened.
    Add-PathEntry -PathEntry (Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Links")

    $knownToolPaths = @(
        "C:\Program Files\CMake\bin",
        (Join-Path $env:LOCALAPPDATA "Android\Sdk\platform-tools"),
        (Join-Path $env:LOCALAPPDATA "Android\Sdk\cmdline-tools\latest\bin"),
        (Join-Path $env:LOCALAPPDATA "Android\Sdk\emulator")
    )

    foreach ($path in $knownToolPaths) {
        Add-PathEntry -PathEntry $path
    }

    $jdkBins = Get-ChildItem "C:\Program Files\Microsoft" -Directory -Filter "jdk-*" -ErrorAction SilentlyContinue |
        ForEach-Object { Join-Path $_.FullName "bin" }
    foreach ($jdkBin in $jdkBins) {
        Add-PathEntry -PathEntry $jdkBin
    }

    $platformToolsDirs = Get-ChildItem (Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Packages") -Directory -Filter "Google.PlatformTools_*" -ErrorAction SilentlyContinue |
        ForEach-Object { Join-Path $_.FullName "platform-tools" }
    foreach ($dir in $platformToolsDirs) {
        Add-PathEntry -PathEntry $dir
    }

    $ninjaDirs = Get-ChildItem (Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Packages") -Directory -Filter "Ninja-build.Ninja_*" -ErrorAction SilentlyContinue |
        ForEach-Object { $_.FullName }
    foreach ($dir in $ninjaDirs) {
        Add-PathEntry -PathEntry $dir
    }
}

function Resolve-JavaHome {
    if (-not [string]::IsNullOrWhiteSpace($env:JAVA_HOME) -and (Test-Path $env:JAVA_HOME)) {
        return $env:JAVA_HOME
    }

    $javaCommand = Get-Command java -ErrorAction SilentlyContinue
    if ($javaCommand -and (Test-Path $javaCommand.Source)) {
        $javaBinDir = Split-Path -Parent $javaCommand.Source
        $candidate = Split-Path -Parent $javaBinDir
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    $jdkCandidates = Get-ChildItem "C:\Program Files\Microsoft" -Directory -Filter "jdk-*" -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending
    if ($jdkCandidates.Count -gt 0) {
        return $jdkCandidates[0].FullName
    }

    return $null
}

function Resolve-AndroidSdkRoot {
    param([string]$RepoLocalPropertiesPath)

    $candidates = @()
    if (-not [string]::IsNullOrWhiteSpace($env:ANDROID_SDK_ROOT)) {
        $candidates += $env:ANDROID_SDK_ROOT
    }
    if (-not [string]::IsNullOrWhiteSpace($env:ANDROID_HOME)) {
        $candidates += $env:ANDROID_HOME
    }
    if (Test-Path $RepoLocalPropertiesPath) {
        $sdkLine = Select-String -Path $RepoLocalPropertiesPath -Pattern "^sdk\.dir=(.+)$" -ErrorAction SilentlyContinue
        if ($sdkLine) {
            $candidates += (($sdkLine.Matches[0].Groups[1].Value) -replace "\\\\", "\\" -replace "C\\:", "C:")
        }
    }

    $candidates += (Join-Path $env:LOCALAPPDATA "Android\Sdk")

    $platformToolsDirs = Get-ChildItem (Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Packages") -Directory -Filter "Google.PlatformTools_*" -ErrorAction SilentlyContinue |
        ForEach-Object { Join-Path $_.FullName "platform-tools" }
    foreach ($ptDir in $platformToolsDirs) {
        if (Test-Path $ptDir) {
            $candidates += (Split-Path -Parent $ptDir)
        }
    }

    return $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
}

function Ensure-AndroidSdkRoot {
    param([string]$CurrentSdkRoot)

    if (-not [string]::IsNullOrWhiteSpace($CurrentSdkRoot) -and (Test-Path $CurrentSdkRoot)) {
        return $CurrentSdkRoot
    }

    $defaultSdkRoot = Join-Path $env:LOCALAPPDATA "Android\Sdk"
    if (-not (Test-Path $defaultSdkRoot)) {
        New-Item -ItemType Directory -Path $defaultSdkRoot -Force | Out-Null
    }

    return $defaultSdkRoot
}

function Bootstrap-AndroidCommandLineTools {
    param(
        [string]$SdkRoot,
        [int]$TimeoutSeconds = 900
    )

    $startTime = Get-Date

    try {
        $sdkManagerBat = Join-Path $SdkRoot "cmdline-tools\latest\bin\sdkmanager.bat"
        if (Test-Path $sdkManagerBat) {
            Add-PathEntry -PathEntry (Split-Path -Parent $sdkManagerBat)
            return New-InstallationResult -PrerequisiteName "android-cmdline-tools" -Success $true -Duration ((Get-Date) - $startTime)
        }

        $tempDir = Join-Path $env:TEMP ("ndi-prereq-cli-" + [guid]::NewGuid().ToString("N"))
        $zipPath = Join-Path $tempDir "commandlinetools-win.zip"
        $extractPath = Join-Path $tempDir "extract"

        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
        New-Item -ItemType Directory -Path $extractPath -Force | Out-Null

        $downloadUrl = "https://dl.google.com/android/repository/commandlinetools-win-11076708_latest.zip"
        Write-InstallationLog "Downloading Android command-line tools from $downloadUrl"
        Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath -TimeoutSec $TimeoutSeconds

        Expand-Archive -Path $zipPath -DestinationPath $extractPath -Force

        $sourceDir = Join-Path $extractPath "cmdline-tools"
        if (-not (Test-Path $sourceDir)) {
            throw "Unexpected archive layout: cmdline-tools folder not found"
        }

        $cmdlineToolsRoot = Join-Path $SdkRoot "cmdline-tools"
        $latestDir = Join-Path $cmdlineToolsRoot "latest"

        New-Item -ItemType Directory -Path $cmdlineToolsRoot -Force | Out-Null
        if (Test-Path $latestDir) {
            Remove-Item -Path $latestDir -Recurse -Force
        }

        Move-Item -Path $sourceDir -Destination $latestDir -Force
        Add-PathEntry -PathEntry (Join-Path $latestDir "bin")

        Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
        return New-InstallationResult -PrerequisiteName "android-cmdline-tools" -Success $true -Duration ((Get-Date) - $startTime)
    }
    catch {
        $duration = (Get-Date) - $startTime
        return New-InstallationResult -PrerequisiteName "android-cmdline-tools" -Success $false -ErrorMessage $_.Exception.Message -Duration $duration
    }
}

function Install-WingetPackage {
    param(
        [string]$PackageId,
        [int]$MaxRetries = 2
    )

    $startTime = Get-Date
    Write-InstallationLog "Installing package with winget: $PackageId"

    for ($attempt = 1; $attempt -le $MaxRetries; $attempt++) {
        try {
            $arguments = @(
                "install",
                "--id", $PackageId,
                "--exact",
                "--silent",
                "--accept-source-agreements",
                "--accept-package-agreements",
                "--disable-interactivity"
            )

            $process = Start-Process -FilePath "winget" -ArgumentList $arguments -NoNewWindow -Wait -PassThru
            if ($process.ExitCode -eq 0) {
                $duration = (Get-Date) - $startTime
                Write-InstallationLog "Installed winget package: $PackageId"
                return New-InstallationResult -PrerequisiteName $PackageId -Success $true -Duration $duration
            }

            throw "winget exited with code $($process.ExitCode)"
        }
        catch {
            $errorMessage = $_.Exception.Message
            Write-InstallationLog "winget attempt $attempt failed for ${PackageId}: $errorMessage" "ERROR"
            if ($attempt -eq $MaxRetries) {
                $duration = (Get-Date) - $startTime
                return New-InstallationResult -PrerequisiteName $PackageId -Success $false -ErrorMessage $errorMessage -Duration $duration
            }
        }
    }
}

function Install-MissingCommandPrerequisites {
    param(
        [string[]]$MissingCommands
    )

    $installResults = @()
    if (-not (Test-WingetAvailable)) {
        Write-InstallationLog "winget is not available. Cannot install missing command prerequisites automatically." "ERROR"
        return ,(New-InstallationResult -PrerequisiteName "winget" -Success $false -ErrorMessage "winget not found on PATH")
    }

    # Candidate package IDs are ordered by preference.
    $commandToPackages = @{
        "java" = @("Microsoft.OpenJDK.21")
        "javac" = @("Microsoft.OpenJDK.21")
        "cmake" = @("Kitware.CMake")
        "ninja" = @("Ninja-build.Ninja")
        "adb" = @("Google.PlatformTools", "Google.AndroidStudio")
        "sdkmanager" = @("Google.AndroidStudio")
        "avdmanager" = @("Google.AndroidStudio")
        "emulator" = @("Google.AndroidStudio")
    }

    $uniquePackages = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($command in $MissingCommands) {
        if ($commandToPackages.ContainsKey($command)) {
            foreach ($candidate in $commandToPackages[$command]) {
                [void]$uniquePackages.Add($candidate)
            }
        }
    }

    foreach ($packageId in $uniquePackages) {
        $result = Install-WingetPackage -PackageId $packageId
        $installResults += $result
    }

    Update-ProcessPathFromSystem
    return $installResults
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
$autoFixConfirmed = $CiMode
$repoLocalProperties = Join-Path $PSScriptRoot "..\local.properties"

Update-ProcessPathFromSystem

$resolvedJavaHome = Resolve-JavaHome
if ($resolvedJavaHome) {
    $env:JAVA_HOME = $resolvedJavaHome
    Add-PathEntry -PathEntry (Join-Path $resolvedJavaHome "bin")
}

# Try to set up Android SDK paths from known locations.
$androidSdkRoot = Resolve-AndroidSdkRoot -RepoLocalPropertiesPath $repoLocalProperties
if ($androidSdkRoot) {
    $env:ANDROID_SDK_ROOT = $androidSdkRoot
    $env:ANDROID_HOME = $androidSdkRoot
}

if ($androidSdkRoot -and (Test-Path $androidSdkRoot)) {
    $pathsAdded = $false
    
    # Candidate paths where Android tools might be located
    $candidates = @(
        (Join-Path $androidSdkRoot "platform-tools"),
        (Join-Path $androidSdkRoot "cmdline-tools\latest\bin"),
        (Join-Path $androidSdkRoot "cmdline-tools\bin"),
        (Join-Path $androidSdkRoot "tools\bin"),
        (Join-Path $androidSdkRoot "emulator")
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

$requiredCommands = @("java", "javac", "adb", "cmake", "ninja")
$optionalCommands = @("sdkmanager", "avdmanager", "emulator")
foreach ($command in $requiredCommands) {
    $available = Test-CommandAvailable -Name $command
    $detail = if ($available) { "Found on PATH" } else { "Missing from PATH" }
    $results += Add-Result -Name "command:$command" -Ok $available -Detail $detail
}

foreach ($command in $optionalCommands) {
    $available = Test-CommandAvailable -Name $command
    $detail = if ($available) { "Found on PATH" } else { "Missing from PATH (optional for local build)" }
    $results += Add-Result -Name "optional-command:$command" -Ok $available -Detail $detail
}

$missingRequiredCommands = @(
    $results |
        Where-Object { -not $_.Ok -and $_.Name -like "command:*" } |
        ForEach-Object { $_.Name.Substring("command:".Length) }
)

$missingOptionalCommands = @(
    $results |
        Where-Object { -not $_.Ok -and $_.Name -like "optional-command:*" } |
        ForEach-Object { $_.Name.Substring("optional-command:".Length) }
)

if (-not $NoInstall -and -not $autoFixConfirmed -and -not $SkipExecution -and ($missingRequiredCommands.Count -gt 0 -or $missingOptionalCommands.Count -gt 0)) {
    Write-Host ""
    Write-Host "Missing prerequisites detected:" -ForegroundColor Yellow
    foreach ($missing in $missingRequiredCommands) {
        Write-Host " - $missing"
    }
    foreach ($missing in $missingOptionalCommands) {
        Write-Host " - $missing (android optional tool)"
    }

    $confirmation = Read-Host "Install missing software now using winget? (Y/N)"
    $autoFixConfirmed = $confirmation -match "^(y|yes)$"
}

if (-not $NoInstall -and $autoFixConfirmed -and ($missingRequiredCommands.Count -gt 0 -or $missingOptionalCommands.Count -gt 0)) {
    if ($missingRequiredCommands.Count -gt 0) {
        $wingetInstallResults = Install-MissingCommandPrerequisites -MissingCommands $missingRequiredCommands
        $installationResults += $wingetInstallResults
    }

    Update-ProcessPathFromSystem

    $resolvedJavaHome = Resolve-JavaHome
    if ($resolvedJavaHome) {
        $env:JAVA_HOME = $resolvedJavaHome
    }

    $androidSdkRoot = Resolve-AndroidSdkRoot -RepoLocalPropertiesPath $repoLocalProperties
    if ($androidSdkRoot) {
        $env:ANDROID_SDK_ROOT = $androidSdkRoot
        $env:ANDROID_HOME = $androidSdkRoot
    }

    $stillMissingOptionalCommands = @(
        $optionalCommands | Where-Object { -not (Test-CommandAvailable -Name $_) }
    )

    if ($stillMissingOptionalCommands.Count -gt 0) {
        $androidSdkRoot = Ensure-AndroidSdkRoot -CurrentSdkRoot $androidSdkRoot
        $env:ANDROID_SDK_ROOT = $androidSdkRoot
        $env:ANDROID_HOME = $androidSdkRoot

        $cmdlineBootstrapResult = Bootstrap-AndroidCommandLineTools -SdkRoot $androidSdkRoot
        $installationResults += $cmdlineBootstrapResult

        Update-ProcessPathFromSystem
        Add-PathEntry -PathEntry (Join-Path $androidSdkRoot "cmdline-tools\latest\bin")
        Add-PathEntry -PathEntry (Join-Path $androidSdkRoot "emulator")
        Add-PathEntry -PathEntry (Join-Path $androidSdkRoot "platform-tools")
    }

    foreach ($command in $missingRequiredCommands) {
        $availableAfterInstall = Test-CommandAvailable -Name $command
        $detailAfterInstall = if ($availableAfterInstall) { "Installed/found on PATH" } else { "Still missing from PATH" }

        $existingResult = $results | Where-Object { $_.Name -eq "command:$command" } | Select-Object -First 1
        if (-not $existingResult) {
            $existingResult = $results | Where-Object { $_.Name -eq "optional-command:$command" } | Select-Object -First 1
        }
        if ($existingResult) {
            $existingResult.Ok = $availableAfterInstall
            $existingResult.Detail = $detailAfterInstall
        }
    }

    foreach ($command in $optionalCommands) {
        $availableAfterInstall = Test-CommandAvailable -Name $command
        $detailAfterInstall = if ($availableAfterInstall) { "Installed/found on PATH" } else { "Still missing from PATH (optional for local build)" }

        $existingResult = $results | Where-Object { $_.Name -eq "optional-command:$command" } | Select-Object -First 1
        if ($existingResult) {
            $existingResult.Ok = $availableAfterInstall
            $existingResult.Detail = $detailAfterInstall
        }
    }
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

$javaHome = Resolve-JavaHome
if ($javaHome) {
    $env:JAVA_HOME = $javaHome
}
$androidSdkRoot = Resolve-AndroidSdkRoot -RepoLocalPropertiesPath $repoLocalProperties
if ($androidSdkRoot) {
    $env:ANDROID_SDK_ROOT = $androidSdkRoot
    $env:ANDROID_HOME = $androidSdkRoot
}

$results += Add-Result -Name "env:JAVA_HOME" -Ok (-not [string]::IsNullOrWhiteSpace($javaHome)) -Detail ($(if ($javaHome) { $javaHome } else { "Unset" }))
$results += Add-Result -Name "env:ANDROID_SDK_ROOT" -Ok (-not [string]::IsNullOrWhiteSpace($androidSdkRoot)) -Detail ($(if ($androidSdkRoot) { $androidSdkRoot } else { "Unset" }))

$ndiSdkCandidates = @()
if (-not [string]::IsNullOrWhiteSpace($env:NDI_SDK_DIR)) {
    $ndiSdkCandidates += $env:NDI_SDK_DIR
}

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

$androidPackages = @("platform-tools", "platforms;android-34", "build-tools;34.0.0", "emulator")
if ($androidSdkRoot) {
    $canManageAndroidPackages = Test-CommandAvailable -Name "sdkmanager"
    foreach ($package in $androidPackages) {
        $packagePath = Join-Path $androidSdkRoot ($package -replace ";", "\\")
        $exists = Test-Path $packagePath

        if (-not $exists -and -not $NoInstall -and $autoFixConfirmed -and $canManageAndroidPackages) {
            Write-InstallationLog "Package $package missing, attempting installation"
            $installResult = Install-AndroidPackage -packageName $package
            $installationResults += $installResult
            $exists = $installResult.Success
        }

        if (-not $exists -and -not $canManageAndroidPackages) {
            $results += Add-Result -Name "optional-package:$package" -Ok $false -Detail "Missing and sdkmanager is not available on PATH yet"
            continue
        }

        $results += Add-Result -Name "package:$package" -Ok $exists -Detail $packagePath
    }
}

$failed = $results | Where-Object {
    -not $_.Ok -and $_.Name -notlike "optional-command:*" -and $_.Name -notlike "optional-package:*"
}

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
