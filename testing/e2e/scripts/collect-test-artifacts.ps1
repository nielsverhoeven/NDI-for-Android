param(
    [string]$SessionId = (Get-Date -Format "yyyyMMdd-HHmmss"),
    [string[]]$EmulatorIds = @("emulator-5554", "emulator-5556"),
    [string]$ArtifactsRoot = "testing/e2e/artifacts",
    [string]$RelayMetricsPath = "testing/e2e/artifacts/runtime/relay-metrics.json",
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"

. "$PSScriptRoot/helpers/result-handler.ps1"
. "$PSScriptRoot/helpers/emulator-adb.ps1"

function Collect-Logcat {
    param([Parameter(Mandatory = $true)][string]$EmulatorSerial, [Parameter(Mandatory = $true)][string]$Directory)
    $path = Join-Path $Directory "logcat-$EmulatorSerial.log"
    Collect-LogcatSnapshot -Serial $EmulatorSerial -OutputPath $path -LineCount 500
    return $path
}

function Collect-ScreenRecording {
    param([Parameter(Mandatory = $true)][string]$EmulatorSerial, [Parameter(Mandatory = $true)][string]$Directory)
    $path = Join-Path $Directory "screenrecord-$EmulatorSerial.mp4"
    # Placeholder artifact for deterministic CI diagnostics when active recordings are unavailable.
    Set-Content -LiteralPath $path -Value "screenrecord-not-captured-in-post-collection" -Encoding UTF8
    return $path
}

function Collect-Diagnostics {
    param([Parameter(Mandatory = $true)][string[]]$Devices, [Parameter(Mandatory = $true)][string]$Directory)

    $diagPath = Join-Path $Directory "diagnostics.json"
    $payload = [PSCustomObject]@{
        capturedAt = (Get-Date).ToUniversalTime().ToString("o")
        devices = @()
        relayMetricsPath = $RelayMetricsPath
        relayMetricsExists = (Test-Path -LiteralPath $RelayMetricsPath)
    }

    foreach ($d in $Devices) {
        $payload.devices += Get-EmulatorStateSnapshot -Serial $d
    }

    $payload | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $diagPath -Encoding UTF8
    return $diagPath
}

function Generate-ArtifactManifest {
    param(
        [Parameter(Mandatory = $true)][string]$Directory,
        [Parameter(Mandatory = $true)][string[]]$Paths
    )

    $manifest = [PSCustomObject]@{
        sessionId = $SessionId
        generatedAt = (Get-Date).ToUniversalTime().ToString("o")
        artifacts = @()
    }

    foreach ($path in $Paths) {
        $item = Get-Item -LiteralPath $path
        $hash = Get-FileHash -LiteralPath $path -Algorithm SHA256
        $manifest.artifacts += [PSCustomObject]@{
            path = $path
            sizeBytes = $item.Length
            checksumSha256 = $hash.Hash
        }
    }

    $manifestPath = Join-Path $Directory "manifest.json"
    $manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
    return $manifestPath
}

try {
    $sessionDir = Join-Path $ArtifactsRoot $SessionId
    New-Item -ItemType Directory -Path $sessionDir -Force | Out-Null

    $artifactPaths = @()
    foreach ($device in $EmulatorIds) {
        $artifactPaths += Collect-Logcat -EmulatorSerial $device -Directory $sessionDir
        $artifactPaths += Collect-ScreenRecording -EmulatorSerial $device -Directory $sessionDir
    }

    $artifactPaths += Collect-Diagnostics -Devices $EmulatorIds -Directory $sessionDir
    $manifestPath = Generate-ArtifactManifest -Directory $sessionDir -Paths $artifactPaths
    $artifactPaths += $manifestPath

    $result = New-E2eResult -Operation "Collect-TestArtifacts" -Status "SUCCESS" -Data ([PSCustomObject]@{
            sessionId = $SessionId
            artifactDirectory = $sessionDir
            artifacts = $artifactPaths
            manifestPath = $manifestPath
        })

    if (-not $OutputPath) {
        $OutputPath = Join-Path $sessionDir "collection-result.json"
    }

    Exit-E2eWithResult -Result $result -OutputPath $OutputPath
}
catch {
    $result = New-E2eResult -Operation "Collect-TestArtifacts" -Status "FAILURE" -Errors @(
        New-E2eError -Code "COLLECTION_FAILED" -Message $_.Exception.Message
    )
    Exit-E2eWithResult -Result $result -OutputPath $OutputPath
}
