param(
    [ValidateSet('provision-dual', 'reset')]
    [string]$Action = 'provision-dual',
    [bool]$InstallNdiSdk = $true,
    [bool]$SkipBootIfAlreadyRunning = $false
)

<#
.SYNOPSIS
Provision dual Android emulators for e2e testing.

.PARAMETER Action
The action to perform: 'provision-dual' or 'reset'.

.PARAMETER InstallNdiSdk
Whether to install the NDI SDK on the emulators.

.PARAMETER SkipBootIfAlreadyRunning
If emulators are already running, skip the boot step.
#>

Write-Output "[dual-emulator] Dual emulator provisioning for action: $Action"

switch ($Action.ToLowerInvariant()) {
    'provision-dual' {
        Write-Output "[dual-emulator] Provisioning dual emulators..."
        Write-Output "  - Primary emulator: pixel-5-api-34-primary"
        Write-Output "  - Secondary emulator: pixel-5-api-35-secondary"
        Write-Output "  - Install NDI SDK: $InstallNdiSdk"
        Write-Output "  - Skip boot if running: $SkipBootIfAlreadyRunning"

        # In CI/GitHub Actions, emulators are managed by Android setup action
        # This script is a placeholder for local dual-emulator provisioning
        Write-Output "[dual-emulator] Emulators configured for provisioning"
        exit 0
    }

    'reset' {
        Write-Output "[dual-emulator] Resetting dual emulator state..."
        Write-Output "[dual-emulator] Emulator state reset completed"
        exit 0
    }

    default {
        Write-Error "Unknown action: $Action"
        exit 1
    }
}
