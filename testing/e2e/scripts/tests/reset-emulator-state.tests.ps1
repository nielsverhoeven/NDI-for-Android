Describe "reset-emulator-state script" {
    It "defines Reset-EmulatorState function" {
        $content = Get-Content -LiteralPath "$PSScriptRoot/../reset-emulator-state.ps1" -Raw
        $content | Should Match "function Reset-EmulatorState"
    }

    It "clears package data through pm clear" {
        $content = Get-Content -LiteralPath "$PSScriptRoot/../reset-emulator-state.ps1" -Raw
        $content | Should Match "pm"
        $content | Should Match "clear"
    }
}
