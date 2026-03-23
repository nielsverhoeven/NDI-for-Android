Set-StrictMode -Version Latest

function New-E2eError {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Code,
        [Parameter(Mandatory = $true)]
        [string]$Message,
        [object]$Details = $null
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
        [ValidateSet("SUCCESS", "PARTIAL_SUCCESS", "FAILURE", "HEALTHY", "DEGRADED", "UNHEALTHY")]
        [string]$Status = "SUCCESS",
        [object]$Data = $null,
        [object[]]$Errors = @(),
        [object[]]$Warnings = @()
    )

    return [PSCustomObject]@{
        operation = $Operation
        status = $Status
        timestamp = (Get-Date).ToUniversalTime().ToString("o")
        data = $Data
        errors = @($Errors)
        warnings = @($Warnings)
    }
}

function Write-E2eJsonResult {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Result,
        [string]$OutputPath
    )

    $json = $Result | ConvertTo-Json -Depth 10
    if ([string]::IsNullOrWhiteSpace($OutputPath)) {
        $json
        return
    }

    $directory = Split-Path -Parent $OutputPath
    if ($directory -and -not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    Set-Content -LiteralPath $OutputPath -Value $json -Encoding UTF8
    $json
}

function Exit-E2eWithResult {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Result,
        [string]$OutputPath,
        [int]$SuccessExitCode = 0,
        [int]$FailureExitCode = 1
    )

    Write-E2eJsonResult -Result $Result -OutputPath $OutputPath | Write-Output

    if ($Result.status -eq "FAILURE" -or $Result.status -eq "UNHEALTHY") {
        exit $FailureExitCode
    }

    exit $SuccessExitCode
}
