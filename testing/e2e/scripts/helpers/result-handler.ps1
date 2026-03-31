function Normalize-E2eStatus {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Status
    )

    $normalized = $Status.Trim().ToLowerInvariant()
    switch ($normalized) {
        'pass' { return 'pass' }
        'fail' { return 'fail' }
        'blocked' { return 'blocked' }
        'not-applicable' { return 'not-applicable' }
        default { throw "Unsupported e2e status: $Status" }
    }
}

function Get-GateDecision {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Status,
        [Parameter(Mandatory = $true)]
        [bool]$RequiredProfile
    )

    $normalized = Normalize-E2eStatus -Status $Status
    if (-not $RequiredProfile) {
        return 'pass'
    }

    if ($normalized -eq 'fail' -or $normalized -eq 'blocked') {
        return 'fail'
    }

    return 'pass'
}

function New-E2eError {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Code,
        [Parameter(Mandatory = $true)]
        [string]$Message,
        [hashtable]$Details
    )

    return [PSCustomObject]@{
        code = $Code
        message = $Message
        details = $Details
    }
}

function New-E2eResult {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Operation,
        [Parameter(Mandatory = $true)]
        [string]$Status,
        [Parameter(Mandatory = $true)]
        [object]$Data,
        [object[]]$Errors = @()
    )

    return [PSCustomObject]@{
        operation = $Operation
        status = Normalize-E2eStatus -Status ($(if ($Status -eq 'SUCCESS') { 'pass' } elseif ($Status -eq 'FAILURE') { 'fail' } else { $Status }))
        data = $Data
        errors = $Errors
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    }
}

function Exit-E2eWithResult {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Result,
        [Parameter(Mandatory = $true)]
        [string]$OutputPath
    )

    $parent = Split-Path -Parent $OutputPath
    if ($parent -and -not (Test-Path $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }

    $Result | ConvertTo-Json -Depth 8 | Set-Content -Path $OutputPath -Encoding UTF8
    if ($Result.status -eq 'fail') {
        exit 1
    }
    exit 0
}
