function Test-ReliabilityWindow {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Outcomes,
        [int]$WindowSize = 20,
        [double]$MinimumRate = 0.95
    )

    if ($Outcomes.Count -lt $WindowSize) {
        throw "Need at least $WindowSize outcomes to evaluate reliability window."
    }

    $window = $Outcomes[($Outcomes.Count - $WindowSize)..($Outcomes.Count - 1)]
    $clean = ($window | Where-Object { $_ -eq 'pass' -or $_ -eq 'not-applicable' }).Count
    $rate = [math]::Round(($clean / $WindowSize), 4)

    [ordered]@{
        windowSize = $WindowSize
        minimumRate = $MinimumRate
        cleanRuns = $clean
        measuredRate = $rate
        passed = ($rate -ge $MinimumRate)
    }
}
