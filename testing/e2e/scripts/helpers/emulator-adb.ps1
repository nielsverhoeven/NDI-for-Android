Set-StrictMode -Version Latest

function Get-AdbExecutable {
    if ($env:ANDROID_SDK_ROOT) {
        $candidate = Join-Path $env:ANDROID_SDK_ROOT "platform-tools/adb.exe"
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
        $candidate = Join-Path $env:ANDROID_SDK_ROOT "platform-tools/adb"
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    return "adb"
}

function Invoke-Adb {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [switch]$AllowFailure
    )

    $adb = Get-AdbExecutable
    $output = & $adb @Arguments 2>&1
    if (-not $AllowFailure -and $LASTEXITCODE -ne 0) {
        throw "adb command failed: $adb $($Arguments -join ' ')`n$output"
    }
    return ($output -join "`n")
}

function Test-AdbDeviceConnected {
    param([Parameter(Mandatory = $true)][string]$Serial)

    $state = Invoke-Adb -Arguments @("-s", $Serial, "get-state") -AllowFailure
    return $state.Trim() -eq "device"
}

function Wait-EmulatorBoot {
    param(
        [Parameter(Mandatory = $true)][string]$Serial,
        [int]$TimeoutSeconds = 90
    )

    $started = Get-Date
    while (((Get-Date) - $started).TotalSeconds -lt $TimeoutSeconds) {
        if (Test-AdbDeviceConnected -Serial $Serial) {
            $boot = (Invoke-Adb -Arguments @("-s", $Serial, "shell", "getprop", "sys.boot_completed") -AllowFailure).Trim()
            if ($boot -eq "1") {
                return $true
            }
        }
        Start-Sleep -Seconds 2
    }

    return $false
}

function Wait-ForEmulatorReady {
    param(
        [Parameter(Mandatory = $true)][string]$Serial,
        [int]$TimeoutSeconds = 60
    )

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $reachable = $false

    while ($stopwatch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        if (Test-AdbDeviceConnected -Serial $Serial) {
            $reachable = $true
            $boot = (Invoke-Adb -Arguments @("-s", $Serial, "shell", "getprop", "sys.boot_completed") -AllowFailure).Trim()
            if ($boot -eq "1") {
                return [PSCustomObject]@{
                    reachable = $true
                    ready = $true
                    readinessWaitMs = [int]$stopwatch.ElapsedMilliseconds
                }
            }
        }

        Start-Sleep -Seconds 2
    }

    return [PSCustomObject]@{
        reachable = $reachable
        ready = $false
        readinessWaitMs = [int]$stopwatch.ElapsedMilliseconds
    }
}

function Get-EmulatorStateSnapshot {
    param([Parameter(Mandatory = $true)][string]$Serial)

    $state = Invoke-Adb -Arguments @("-s", $Serial, "get-state") -AllowFailure
    $boot = Invoke-Adb -Arguments @("-s", $Serial, "shell", "getprop", "sys.boot_completed") -AllowFailure
    $api = Invoke-Adb -Arguments @("-s", $Serial, "shell", "getprop", "ro.build.version.sdk") -AllowFailure

    return [PSCustomObject]@{
        serial = $Serial
        adbState = $state.Trim()
        bootCompleted = ($boot.Trim() -eq "1")
        apiLevel = [int]($api.Trim() -replace "[^0-9]", "")
        sampledAt = (Get-Date).ToUniversalTime().ToString("o")
    }
}

function Install-ApkToEmulator {
    param(
        [Parameter(Mandatory = $true)][string]$Serial,
        [Parameter(Mandatory = $true)][string]$ApkPath
    )

    if (-not (Test-Path -LiteralPath $ApkPath)) {
        throw "APK not found: $ApkPath"
    }

    Invoke-Adb -Arguments @("-s", $Serial, "install", "-r", $ApkPath) | Out-Null
}

function Get-InstalledAppVersion {
    param(
        [Parameter(Mandatory = $true)][string]$Serial,
        [Parameter(Mandatory = $true)][string]$PackageName
    )

    $output = Invoke-Adb -Arguments @("-s", $Serial, "shell", "dumpsys", "package", $PackageName) -AllowFailure
    $versionNameMatch = [regex]::Match($output, "versionName=(?<name>[^\r\n]+)")
    $versionCodeMatch = [regex]::Match($output, "versionCode=(?<code>\d+)")

    $versionName = if ($versionNameMatch.Success) { $versionNameMatch.Groups["name"].Value.Trim() } else { $null }
    $versionCode = if ($versionCodeMatch.Success) { [int]$versionCodeMatch.Groups["code"].Value } else { $null }

    return [PSCustomObject]@{
        versionName = $versionName
        versionCode = $versionCode
    }
}

function Test-AppLaunchable {
    param(
        [Parameter(Mandatory = $true)][string]$Serial,
        [Parameter(Mandatory = $true)][string]$PackageName,
        [Parameter(Mandatory = $true)][string]$ActivityName
    )

    $component = "$PackageName/$ActivityName"
    $output = Invoke-Adb -Arguments @("-s", $Serial, "shell", "am", "start", "-W", "-n", $component) -AllowFailure
    return $output -match "Status:\s*ok"
}

function Start-EmulatorRecording {
    param(
        [Parameter(Mandatory = $true)][string]$Serial,
        [Parameter(Mandatory = $true)][string]$RemotePath,
        [int]$BitRateMbps = 4,
        [int]$TimeLimitSeconds = 180
    )

    $bitRate = $BitRateMbps * 1000000
    Invoke-Adb -Arguments @("-s", $Serial, "shell", "screenrecord", "--time-limit", "$TimeLimitSeconds", "--bit-rate", "$bitRate", $RemotePath) -AllowFailure | Out-Null
}

function Stop-Emulator {
    param([Parameter(Mandatory = $true)][string]$Serial)

    Invoke-Adb -Arguments @("-s", $Serial, "emu", "kill") -AllowFailure | Out-Null
}

function Collect-LogcatSnapshot {
    param(
        [Parameter(Mandatory = $true)][string]$Serial,
        [Parameter(Mandatory = $true)][string]$OutputPath,
        [int]$LineCount = 500
    )

    $directory = Split-Path -Parent $OutputPath
    if ($directory -and -not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $output = Invoke-Adb -Arguments @("-s", $Serial, "logcat", "-d", "-t", "$LineCount") -AllowFailure
    Set-Content -LiteralPath $OutputPath -Value $output -Encoding UTF8
}
