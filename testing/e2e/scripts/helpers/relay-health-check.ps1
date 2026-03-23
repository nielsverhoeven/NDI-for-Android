Set-StrictMode -Version Latest

function Fill-RandomBytes {
    param([Parameter(Mandatory = $true)][byte[]]$Buffer)

    if ([System.Security.Cryptography.RandomNumberGenerator].GetMethod("Fill")) {
        [System.Security.Cryptography.RandomNumberGenerator]::Fill($Buffer)
        return
    }

    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($Buffer)
    }
    finally {
        $rng.Dispose()
    }
}

function Invoke-RelayEchoCheck {
    param(
        [Parameter(Mandatory = $true)][string]$TargetHost,
        [Parameter(Mandatory = $true)][int]$Port,
        [int]$EchoCount = 5,
        [int]$PayloadBytes = 256,
        [int]$MaxLatencyMs = 100
    )

    $samples = @()
    $errors = @()
    for ($i = 0; $i -lt $EchoCount; $i++) {
        $client = [System.Net.Sockets.TcpClient]::new()
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            $client.Connect($TargetHost, $Port)
            $stream = $client.GetStream()
            $payload = New-Object byte[] $PayloadBytes
            Fill-RandomBytes -Buffer $payload
            $stream.Write($payload, 0, $payload.Length)
            $stream.Flush()

            $recv = New-Object byte[] $PayloadBytes
            [void]$stream.Read($recv, 0, $recv.Length)
            $sw.Stop()
            $samples += [int]$sw.ElapsedMilliseconds
        }
        catch {
            $errors += $_.Exception.Message
        }
        finally {
            $client.Dispose()
        }
    }

    $avg = if ($samples.Count -gt 0) { [int](($samples | Measure-Object -Average).Average) } else { 9999 }
    $peak = if ($samples.Count -gt 0) { [int](($samples | Measure-Object -Maximum).Maximum) } else { 9999 }

    $status = "HEALTHY"
    if ($errors.Count -gt 0 -or $peak -gt $MaxLatencyMs) {
        $status = "UNHEALTHY"
    }
    elseif ($avg -gt [math]::Floor($MaxLatencyMs / 2)) {
        $status = "DEGRADED"
    }

    return [PSCustomObject]@{
        status = $status
        averageLatencyMs = $avg
        peakLatencyMs = $peak
        samples = $samples
        errors = $errors
        checkedAt = (Get-Date).ToUniversalTime().ToString("o")
    }
}
