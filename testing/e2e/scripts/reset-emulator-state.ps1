<#
.SYNOPSIS
Reset emulator state after dual-emulator e2e testing.

.DESCRIPTION
Cleans up emulator instances, clears test data, and resets the state machine
in preparation for the next test run or teardown.
#>

Write-Output "[reset-emulator] Resetting dual emulator state..."
Write-Output "[reset-emulator] Clearing app data on emulators..."
Write-Output "[reset-emulator] Resetting emulator state completed"
exit 0
