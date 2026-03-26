param(
    [string]$ApkPath = "app/build/outputs/apk/debug/app-debug.apk",
    [string]$PackageName = "com.ndi.app.debug",
    [string[]]$Serials,
    [int]$TimeoutSeconds = 60,
    [string]$OutputPath = "testing/e2e/artifacts/runtime/preinstall-report.json",
    [string]$ActivityName = "com.ndi.app.MainActivity"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. "$PSScriptRoot/helpers/emulator-adb.ps1"

function Get-AaptExecutable {
    if ($env:ANDROID_SDK_ROOT) {
        $buildToolsDir = Join-Path $env:ANDROID_SDK_ROOT "build-tools"
        if (Test-Path -LiteralPath $buildToolsDir) {
            $latest = Get-ChildItem -LiteralPath $buildToolsDir -Directory | Sort-Object Name -Descending | Select-Object -First 1
            if ($latest) {
                foreach ($tool in @("aapt2.exe", "aapt.exe", "aapt2", "aapt")) {
                    $candidate = Join-Path $latest.FullName $tool
                    if (Test-Path -LiteralPath $candidate) {
                        return $candidate
                    }
                }
            }
        }
    }

    return "aapt"
}

function Resolve-AbsolutePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return [System.IO.Path]::GetFullPath((Join-Path $RepoRoot $Path))
}

function New-BuildArtifact {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$PackageName,
        [bool]$Exists,
        [string]$VersionName,
        [Nullable[int]]$VersionCode,
        [string]$BuildTimestamp
    )

    $identifier = $null
    if ($VersionName -and $VersionCode) {
        $identifier = "$VersionName+$VersionCode"
    }

    return [PSCustomObject]@{
        path = $Path
        variant = "debug"
        packageName = $PackageName
        versionName = $VersionName
        versionCode = $VersionCode
        versionIdentifier = $identifier
        buildTimestamp = $BuildTimestamp
        exists = $Exists
    }
}

function Get-ApkMetadata {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ApkPath
    )

    $toolsToTry = New-Object System.Collections.Generic.List[string]
    $primaryTool = Get-AaptExecutable
    $toolsToTry.Add($primaryTool)

    if ($primaryTool -like "*aapt.exe") {
        $fallbackAapt2 = Join-Path (Split-Path -Parent $primaryTool) "aapt2.exe"
        if ((Test-Path -LiteralPath $fallbackAapt2) -and -not $toolsToTry.Contains($fallbackAapt2)) {
            $toolsToTry.Add($fallbackAapt2)
        }
    }

    if ($primaryTool -like "*aapt2.exe") {
        $fallbackAapt = Join-Path (Split-Path -Parent $primaryTool) "aapt.exe"
        if ((Test-Path -LiteralPath $fallbackAapt) -and -not $toolsToTry.Contains($fallbackAapt)) {
            $toolsToTry.Add($fallbackAapt)
        }
    }

    $errors = @()
    foreach ($tool in $toolsToTry) {
        $output = & $tool dump badging $ApkPath 2>&1
        $joined = ($output -join "`n")
        $match = [regex]::Match($joined, "package:\s+name='[^']+'\s+versionCode='(?<code>\d+)'\s+versionName='(?<name>[^']+)'")
        if ($match.Success) {
            return [PSCustomObject]@{
                versionName = $match.Groups["name"].Value
                versionCode = [int]$match.Groups["code"].Value
            }
        }

        $errors += "{0}: {1}" -f $tool, $joined
    }

    throw ([string]::Format("Unable to extract APK metadata. Tried tools: {0}", ($errors -join " | ")))
}

function New-DeviceRecord {
    param([Parameter(Mandatory = $true)][string]$Serial)

    return [ordered]@{
        serial = $Serial
        reachable = $false
        ready = $false
        readinessWaitMs = 0
        apkInstalled = $false
        installedVersionName = $null
        installedVersionCode = $null
        installedVersionIdentifier = $null
        launchVerified = $false
        elapsedMs = 0
        status = "UNREACHABLE"
        errorMessage = $null
    }
}

function Write-Report {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$Report,
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot
    )

    $absolutePath = Resolve-AbsolutePath -Path $Path -RepoRoot $RepoRoot
    $dir = Split-Path -Parent $absolutePath
    if ($dir -and -not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    $Report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $absolutePath -Encoding UTF8
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../../..")
$effectiveApkPath = if ($PSBoundParameters.ContainsKey("ApkPath")) { $ApkPath } elseif ($env:APP_APK_PATH) { $env:APP_APK_PATH } else { $ApkPath }
$effectiveSerials = if ($PSBoundParameters.ContainsKey("Serials") -and $Serials.Count -gt 0) {
    $Serials
}
else {
    @(
        $(if ($env:EMULATOR_A_SERIAL) { $env:EMULATOR_A_SERIAL } else { "emulator-5554" }),
        $(if ($env:EMULATOR_B_SERIAL) { $env:EMULATOR_B_SERIAL } else { "emulator-5556" })
    )
}
$effectiveSerials = @($effectiveSerials)

$reportStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$deviceRecords = New-Object System.Collections.Generic.List[object]
$buildArtifact = $null
$finalExitCode = 1
$finalReason = $null
$expectedVersionIdentifier = "missing"

try {
    $apkAbsolutePath = Resolve-AbsolutePath -Path $effectiveApkPath -RepoRoot $repoRoot
    $exists = Test-Path -LiteralPath $apkAbsolutePath

    if (-not $exists) {
        $buildArtifact = New-BuildArtifact -Path $apkAbsolutePath -PackageName $PackageName -Exists $false -VersionName $null -VersionCode $null -BuildTimestamp $null
        $finalReason = "APK artifact not found at $effectiveApkPath. Run './gradlew :app:assembleDebug' before executing the e2e test suite."

        $report = [PSCustomObject]@{
            reportId = [guid]::NewGuid().ToString()
            timestamp = (Get-Date).ToUniversalTime().ToString("o")
            buildArtifact = $buildArtifact
            devices = @()
            overallStatus = "FAIL"
            failureReason = $finalReason
            totalElapsedMs = [int]$reportStopwatch.ElapsedMilliseconds
            abortedBeforeInstall = $true
        }

        Write-Report -Report $report -Path $OutputPath -RepoRoot $repoRoot
        Write-Output "PRE-FLIGHT FAIL: $finalReason; expected=missing"
        exit 1
    }

    $metadata = $null
    try {
        $metadata = Get-ApkMetadata -ApkPath $apkAbsolutePath
    }
    catch {
        Write-Warning "Unable to parse APK metadata from '$apkAbsolutePath'. Continuing with install/launch verification only. Details: $($_.Exception.Message)"
    }
    $buildTimestamp = (Get-Item -LiteralPath $apkAbsolutePath).LastWriteTimeUtc.ToString("o")
    $versionName = $null
    $versionCode = $null
    if ($metadata) {
        $versionName = $metadata.versionName
        $versionCode = $metadata.versionCode
    }
    $buildArtifact = New-BuildArtifact -Path $apkAbsolutePath -PackageName $PackageName -Exists $true -VersionName $versionName -VersionCode $versionCode -BuildTimestamp $buildTimestamp
    $expectedVersionIdentifier = $buildArtifact.versionIdentifier

    foreach ($serial in $effectiveSerials) {
        Write-Output "PRE-FLIGHT: $serial readiness check"
        $record = New-DeviceRecord -Serial $serial
        $deviceStopwatch = [System.Diagnostics.Stopwatch]::StartNew()

        $readyResult = Wait-ForEmulatorReady -Serial $serial -TimeoutSeconds $TimeoutSeconds
        $record.reachable = [bool]$readyResult.reachable
        $record.ready = [bool]$readyResult.ready
        $record.readinessWaitMs = [int]$readyResult.readinessWaitMs

        if (-not $record.reachable) {
            $record.status = "UNREACHABLE"
            $record.errorMessage = "{0}: Emulator is not reachable through adb." -f $serial
            $record.elapsedMs = [int]$deviceStopwatch.ElapsedMilliseconds
            $deviceRecords.Add([PSCustomObject]$record)
            continue
        }

        if (-not $record.ready) {
            $record.status = "NOT_READY"
            $record.errorMessage = "{0}: Emulator did not become install-ready within {1} seconds. Verify boot completion and ADB responsiveness." -f $serial, $TimeoutSeconds
            $record.elapsedMs = [int]$deviceStopwatch.ElapsedMilliseconds
            $deviceRecords.Add([PSCustomObject]$record)
            continue
        }

        $remainingSeconds = [Math]::Max(0, $TimeoutSeconds - [Math]::Ceiling($deviceStopwatch.ElapsedMilliseconds / 1000.0))
        if ($remainingSeconds -le 0) {
            $record.status = "TIMEOUT"
            $record.errorMessage = "{0}: Pre-flight budget exceeded before installation could begin." -f $serial
            $record.elapsedMs = [int]$deviceStopwatch.ElapsedMilliseconds
            $deviceRecords.Add([PSCustomObject]$record)
            continue
        }

        Write-Output "PRE-FLIGHT: $serial install start"
        $job = Start-Job -ScriptBlock {
            param($helperPath, $s, $apk)
            Set-StrictMode -Version Latest
            $ErrorActionPreference = "Stop"
            . $helperPath
            Install-ApkToEmulator -Serial $s -ApkPath $apk
        } -ArgumentList (Join-Path $PSScriptRoot "helpers/emulator-adb.ps1"), $serial, $apkAbsolutePath

        if (-not (Wait-Job -Job $job -Timeout $remainingSeconds)) {
            Stop-Job -Job $job -Force | Out-Null
            Remove-Job -Job $job -Force | Out-Null
            $record.status = "TIMEOUT"
            $record.errorMessage = "{0}: APK installation exceeded remaining pre-flight budget." -f $serial
            $record.elapsedMs = [int]$deviceStopwatch.ElapsedMilliseconds
            $deviceRecords.Add([PSCustomObject]$record)
            continue
        }

        $jobFailed = $job.State -ne "Completed"
        $jobOutput = Receive-Job -Job $job -ErrorAction SilentlyContinue
        Remove-Job -Job $job -Force | Out-Null
        if ($jobFailed) {
            $record.status = "INSTALL_FAILED"
            $record.errorMessage = "{0}: APK installation failed. {1}" -f $serial, $jobOutput
            $record.elapsedMs = [int]$deviceStopwatch.ElapsedMilliseconds
            $deviceRecords.Add([PSCustomObject]$record)
            continue
        }

        $record.apkInstalled = $true

        $installed = Get-InstalledAppVersion -Serial $serial -PackageName $PackageName
        $record.installedVersionName = $installed.versionName
        $record.installedVersionCode = $installed.versionCode
        if ($installed.versionName -and $installed.versionCode) {
            $record.installedVersionIdentifier = "$($installed.versionName)+$($installed.versionCode)"
        }

        if ($expectedVersionIdentifier -and $record.installedVersionIdentifier -ne $expectedVersionIdentifier) {
            $record.status = "VERSION_MISMATCH"
            $record.errorMessage = "{0}: Installed version '{1}' does not match expected '{2}'." -f $serial, $record.installedVersionIdentifier, $expectedVersionIdentifier
            $record.elapsedMs = [int]$deviceStopwatch.ElapsedMilliseconds
            $deviceRecords.Add([PSCustomObject]$record)
            continue
        }

        $remainingSeconds = [Math]::Max(0, $TimeoutSeconds - [Math]::Ceiling($deviceStopwatch.ElapsedMilliseconds / 1000.0))
        if ($remainingSeconds -le 0) {
            $record.status = "TIMEOUT"
            $record.errorMessage = "{0}: Launch verification exceeded pre-flight budget before execution." -f $serial
            $record.elapsedMs = [int]$deviceStopwatch.ElapsedMilliseconds
            $deviceRecords.Add([PSCustomObject]$record)
            continue
        }

        $launchOk = Test-AppLaunchable -Serial $serial -PackageName $PackageName -ActivityName $ActivityName
        if (-not $launchOk) {
            $record.status = "LAUNCH_FAILED"
            $record.errorMessage = "{0}: Launch verification failed after successful installation." -f $serial
            $record.elapsedMs = [int]$deviceStopwatch.ElapsedMilliseconds
            $deviceRecords.Add([PSCustomObject]$record)
            continue
        }

        Invoke-Adb -Arguments @("-s", $serial, "shell", "am", "force-stop", $PackageName) -AllowFailure | Out-Null

        $record.launchVerified = $true
        $record.status = "PASS"
        $record.errorMessage = $null
        $record.elapsedMs = [int]$deviceStopwatch.ElapsedMilliseconds
        $deviceRecords.Add([PSCustomObject]$record)
    }

    $failedRecords = @($deviceRecords | Where-Object { $_.status -ne "PASS" })
    $overall = if ($failedRecords.Count -eq 0) { "PASS" } else { "FAIL" }
    $finalReason = if ($overall -eq "PASS") {
        $null
    }
    else {
        $details = ($failedRecords | ForEach-Object { "$($_.serial): $($_.status) - $($_.errorMessage)" }) -join "; "
        "Pre-flight installation failed on $($failedRecords.Count) of $($effectiveSerials.Count) devices. $details"
    }

    $report = [PSCustomObject]@{
        reportId = [guid]::NewGuid().ToString()
        timestamp = (Get-Date).ToUniversalTime().ToString("o")
        buildArtifact = $buildArtifact
        devices = $deviceRecords.ToArray()
        overallStatus = $overall
        failureReason = $finalReason
        totalElapsedMs = [int]$reportStopwatch.ElapsedMilliseconds
        abortedBeforeInstall = $false
    }

    Write-Report -Report $report -Path $OutputPath -RepoRoot $repoRoot

    if ($overall -eq "PASS") {
        $deviceSummary = ($deviceRecords | ForEach-Object { "$($_.serial):$($_.installedVersionIdentifier)" }) -join ","
        Write-Output "PRE-FLIGHT PASS: $($deviceRecords.Count)/$($deviceRecords.Count) devices verified; expected=$expectedVersionIdentifier; devices=$deviceSummary"
        $finalExitCode = 0
    }
    else {
        Write-Output "PRE-FLIGHT FAIL: $finalReason; expected=$expectedVersionIdentifier"
        $finalExitCode = 1
    }
}
catch {
    $reason = "Pre-flight script error: $($_.Exception.Message)"
    if (-not $buildArtifact) {
        $buildArtifact = New-BuildArtifact -Path (Resolve-AbsolutePath -Path $effectiveApkPath -RepoRoot $repoRoot) -PackageName $PackageName -Exists $false -VersionName $null -VersionCode $null -BuildTimestamp $null
    }

    $report = [PSCustomObject]@{
        reportId = [guid]::NewGuid().ToString()
        timestamp = (Get-Date).ToUniversalTime().ToString("o")
        buildArtifact = $buildArtifact
        devices = $deviceRecords.ToArray()
        overallStatus = "FAIL"
        failureReason = $reason
        totalElapsedMs = [int]$reportStopwatch.ElapsedMilliseconds
        abortedBeforeInstall = ($deviceRecords.Count -eq 0)
    }

    Write-Report -Report $report -Path $OutputPath -RepoRoot $repoRoot
    Write-Output "PRE-FLIGHT FAIL: $reason; expected=$expectedVersionIdentifier"
    $finalExitCode = 1
}

exit $finalExitCode
