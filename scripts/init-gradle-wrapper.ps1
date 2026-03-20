#!/usr/bin/env pwsh

# Initialize Gradle Wrapper Script
param(
    [string]$ProjectRoot = "C:\gitrepos\NDI-for-Android"
)

$ErrorActionPreference = "Stop"

Write-Host "Initializing Gradle Wrapper for Android project..."

# Set environment variables
$env:JAVA_HOME = "C:\Program Files\Java\jdk-21.0.10"
$env:ANDROID_SDK_ROOT = "$env:LOCALAPPDATA\Android\Sdk"
$gradle_dists = "$env:GRADLE_USER_HOME\wrapper\dists\gradle-9.2.1-bin"

# Check if gradle is extracted
$gradle_home = Get-ChildItem $gradle_dists -Directory | Where-Object { $_.Name -match "gradle-9.2.1$" } | Select-Object -First 1
if (-not $gradle_home) {
    Write-Error "Gradle not found in extracted location"
    exit 1
}

$gradle_bat = Join-Path $gradle_home.FullName "bin\gradle.bat"
Write-Host "Using Gradle: $gradle_bat"

# Change to project directory
cd $ProjectRoot

# Run gradle wrapper command using the extracted gradle
& $gradle_bat wrapper --gradle-version 9.2.1 --distribution-type bin

# Verify wrapper jar exists
if (Test-Path "gradle\wrapper\gradle-wrapper.jar") {
    Write-Host "✓ Gradle wrapper initialized successfully"
    Write-Host "Wrapper jar: $(Get-Item 'gradle\wrapper\gradle-wrapper.jar' | Select-Object -ExpandProperty FullName)"
    exit 0
} else {
    Write-Error "Gradle wrapper jar not created"
    exit 1
}

