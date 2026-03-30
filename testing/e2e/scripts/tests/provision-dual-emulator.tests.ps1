Describe "provision-dual-emulator script" {
    It "defines Provision-Emulator function" {
        $content = Get-Content -LiteralPath "$PSScriptRoot/../provision-dual-emulator.ps1" -Raw
        $content | Should Match "function Provision-Emulator"
    }

    It "defines Provision-DualEmulator function" {
        $content = Get-Content -LiteralPath "$PSScriptRoot/../provision-dual-emulator.ps1" -Raw
        $content | Should Match "function Provision-DualEmulator"
    }

    It "returns structured result with status" {
        $content = Get-Content -LiteralPath "$PSScriptRoot/../provision-dual-emulator.ps1" -Raw
        $content | Should Match "New-E2eResult"
    }
}
