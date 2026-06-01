function New-TriageSummary {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Status,
        [Parameter(Mandatory = $true)]
        [string[]]$ScenarioIds,
        [Parameter(Mandatory = $true)]
        [string]$RootCauseCategory,
        [Parameter(Mandatory = $true)]
        [string]$OutputPath
    )

    $now = (Get-Date).ToUniversalTime().ToString('o')
    $summary = [ordered]@{
        status = $Status
        scenarioIds = $ScenarioIds
        rootCauseCategory = $RootCauseCategory
        failureTimestampUtc = $now
        firstClassifiedAtUtc = $now
    }

    $summary | ConvertTo-Json -Depth 5 | Set-Content -Path $OutputPath -Encoding UTF8
    return $summary
}
