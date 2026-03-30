Describe "collect-test-artifacts script" {
    It "defines Collect-Logcat" {
        $content = Get-Content -LiteralPath "$PSScriptRoot/../collect-test-artifacts.ps1" -Raw
        $content | Should Match "function Collect-Logcat"
    }

    It "defines Collect-ScreenRecording" {
        $content = Get-Content -LiteralPath "$PSScriptRoot/../collect-test-artifacts.ps1" -Raw
        $content | Should Match "function Collect-ScreenRecording"
    }

    It "defines Collect-Diagnostics" {
        $content = Get-Content -LiteralPath "$PSScriptRoot/../collect-test-artifacts.ps1" -Raw
        $content | Should Match "function Collect-Diagnostics"
    }

    It "defines Generate-ArtifactManifest" {
        $content = Get-Content -LiteralPath "$PSScriptRoot/../collect-test-artifacts.ps1" -Raw
        $content | Should Match "function Generate-ArtifactManifest"
    }
}
