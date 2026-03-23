param(
    [switch]$RunServer,
    [int]$ListenPort = 15000,
    [string]$MetricsPath = "testing/e2e/artifacts/runtime/relay-metrics.json"
)

Set-StrictMode -Version Latest

function Start-TcpEchoRelay {
    param(
        [int]$Port,
        [string]$MetricsOutputPath
    )

    $dir = Split-Path -Parent $MetricsOutputPath
    if ($dir -and -not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
    $listener.Start()

    $metrics = [PSCustomObject]@{
        listeningPort = $Port
        startedAt = (Get-Date).ToUniversalTime().ToString("o")
        totalBytesForwarded = 0
        peakLatencyMs = 0
        avgLatencyMs = 0
        samples = @()
    }

    while ($true) {
        if (-not $listener.Pending()) {
            Start-Sleep -Milliseconds 100
            continue
        }

        $client = $listener.AcceptTcpClient()
        $stream = $client.GetStream()

        try {
            $buffer = New-Object byte[] 4096
            while ($client.Connected) {
                $sw = [System.Diagnostics.Stopwatch]::StartNew()
                $read = $stream.Read($buffer, 0, $buffer.Length)
                if ($read -le 0) {
                    break
                }

                $stream.Write($buffer, 0, $read)
                $stream.Flush()
                $sw.Stop()

                $metrics.totalBytesForwarded += $read
                $metrics.samples += [int]$sw.ElapsedMilliseconds
                if ($sw.ElapsedMilliseconds -gt $metrics.peakLatencyMs) {
                    $metrics.peakLatencyMs = [int]$sw.ElapsedMilliseconds
                }
                if ($metrics.samples.Count -gt 0) {
                    $metrics.avgLatencyMs = [int](($metrics.samples | Measure-Object -Average).Average)
                }
            }
        }
        finally {
            $client.Dispose()
            $metrics | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $MetricsOutputPath -Encoding UTF8
        }
    }
}

if ($RunServer) {
    Start-TcpEchoRelay -Port $ListenPort -MetricsOutputPath $MetricsPath
}
