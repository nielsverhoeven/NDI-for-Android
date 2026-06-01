# Contracts: Prerequisite Installer Interface

**Feature**: 012-fix-prereq-preflight
**Date**: March 23, 2026
**Phase**: 1 (Design & Architecture)

## Overview

Defines the contract for prerequisite installation functionality in the Android prerequisites verification script.

## Interface: PrerequisiteInstaller

### Method: Install-Prerequisite
Installs a missing Android development prerequisite.

**Parameters**:
- `name`: String - Prerequisite name (e.g., "platform-tools")
- `type`: String - Prerequisite type ("package", "command", "env")
- `timeout`: TimeSpan - Maximum installation time (default: 5 minutes)

**Returns**:
- `InstallationResult` - Success status and details

**Behavior**:
- Attempts installation using appropriate method for prerequisite type
- Accepts licenses automatically for SDK packages
- Retries failed installations up to 3 times with exponential backoff
- Times out after specified duration
- Logs progress and errors

**Error Handling**:
- Throws `InstallationException` for permanent failures
- Returns partial success for retryable failures
- Includes actionable error messages

### Method: Test-Prerequisite
Verifies that a prerequisite is properly installed.

**Parameters**:
- `name`: String - Prerequisite name
- `type`: String - Prerequisite type

**Returns**:
- `Boolean` - True if installed and functional

**Behavior**:
- Checks file/directory existence
- Validates version compatibility if applicable
- Tests basic functionality (e.g., command execution)

## Data Types

### InstallationResult
```powershell
class InstallationResult {
    [string]$PrerequisiteName
    [bool]$Success
    [string]$ErrorMessage
    [TimeSpan]$Duration
    [DateTime]$Timestamp
}
```

### InstallationException
```powershell
class InstallationException : Exception {
    [string]$PrerequisiteName
    [string]$ActionableMessage
}
```

## Implementation Requirements

### Package Installation
For Android SDK packages:
- Use `sdkmanager --install "$name"`
- Accept licenses with `yes | sdkmanager --licenses`
- Verify installation with directory existence check

### Command Installation
For missing CLI tools:
- Attempt installation via package managers (choco, winget)
- Fallback to manual instructions
- Update PATH environment

### Environment Variables
For missing environment setup:
- Set required variables
- Persist for current session
- Document permanent setup requirements

## Compatibility

- Must maintain backward compatibility with existing script usage
- Should work in both CI and local development environments
- No breaking changes to existing verification logic</content>
<parameter name="filePath">c:\github\NDI-for-Android\specs\012-fix-prereq-preflight\contracts\prerequisite-installer.md