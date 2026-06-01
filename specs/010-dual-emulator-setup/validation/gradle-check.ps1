param(
    [string]$ExpectedGradle = "9.2.1",
    [string]$ExpectedKotlin = "2.2.10"
)

$ErrorActionPreference = "Stop"

$output = (& ./gradlew.bat --version 2>&1) -join "`n"

$gradleOk = $output -match "Gradle\s+$([regex]::Escape($ExpectedGradle))"
$kotlinOk = $output -match "Kotlin:\s+$([regex]::Escape($ExpectedKotlin))"

$result = [PSCustomObject]@{
    gradleVersionExpected = $ExpectedGradle
    kotlinVersionExpected = $ExpectedKotlin
    gradleVersionMatched = $gradleOk
    kotlinVersionMatched = $kotlinOk
    status = if ($gradleOk -and $kotlinOk) { "SUCCESS" } else { "FAILURE" }
}

$result | ConvertTo-Json -Depth 5 | Write-Output
if ($result.status -eq "FAILURE") {
    exit 1
}
