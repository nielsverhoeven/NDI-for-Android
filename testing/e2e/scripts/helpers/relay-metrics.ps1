Set-StrictMode -Version Latest

function Get-RelayMetrics {
    param([Parameter(Mandatory = $true)][string]$MetricsPath)

    if (-not (Test-Path -LiteralPath $MetricsPath)) {
        return [PSCustomObject]@{
            totalBytesForwarded = 0
            peakLatencyMs = 0
            avgLatencyMs = 0
            samples = @()
        }
    }

    return Get-Content -LiteralPath $MetricsPath -Raw | ConvertFrom-Json
}
