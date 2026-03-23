Describe "start-relay-server script" {
    It "defines Start-RelayServer" {
        $content = Get-Content -LiteralPath "$PSScriptRoot/../start-relay-server.ps1" -Raw
        $content | Should Match "function Start-RelayServer"
    }

    It "defines Get-RelayServer" {
        $content = Get-Content -LiteralPath "$PSScriptRoot/../start-relay-server.ps1" -Raw
        $content | Should Match "function Get-RelayServer"
    }

    It "defines Stop-RelayServer" {
        $content = Get-Content -LiteralPath "$PSScriptRoot/../start-relay-server.ps1" -Raw
        $content | Should Match "function Stop-RelayServer"
    }
}
