# Quickstart: Fix Prereq Preflight

**Feature**: 012-fix-prereq-preflight
**Date**: March 23, 2026
**Phase**: 1 (Design & Architecture)

## Overview

This feature enhances the Android prerequisites verification script to automatically install missing components, ensuring CI builds succeed without manual intervention.

## For CI Maintainers

### Automated Setup
The CI pipeline will automatically install missing prerequisites during the preflight stage. No manual configuration required.

### Monitoring Installation
Check CI logs for installation progress:
```
Installing missing prerequisite: platform-tools
SDK package 'platform-tools' installed successfully in 00:00:12
```

### Troubleshooting Failures
If installation fails, check for:
- Network connectivity issues
- Insufficient disk space
- License acceptance problems

## For Local Development

### Using the Enhanced Script
Run the verification script as before:

```powershell
pwsh ./scripts/verify-android-prereqs.ps1
```

The script will now automatically install missing Android SDK packages.

### Manual Installation Override
To prevent automatic installation (e.g., for testing):

```powershell
pwsh ./scripts/verify-android-prereqs.ps1 -NoInstall
```

### Testing Changes
1. Create a clean Windows environment (VM or fresh CI runner)
2. Remove Android SDK packages manually
3. Run the script and verify automatic installation
4. Check that all prerequisites are properly installed

## Implementation Details

### What Gets Installed
- Android SDK platform-tools
- Android API 34 platform
- Android build-tools 34.0.0

### Installation Process
1. Verify prerequisite existence
2. If missing, download and install using sdkmanager
3. Accept SDK licenses automatically
4. Retry failed installations up to 3 times
5. Continue with normal verification

### Performance Expectations
- Installation time: < 5 minutes
- Network-dependent (Google Maven repositories)
- Cached for subsequent runs in CI

## Validation

### Success Indicators
- CI preflight job passes consistently
- No manual prerequisite setup required
- Clear error messages on failures
- Installation completes within time limits

### Testing Checklist
- [ ] Script runs on clean Windows environment
- [ ] All prerequisites install automatically
- [ ] CI builds pass after installation
- [ ] Error handling works for network failures
- [ ] Local development workflow unchanged</content>
<parameter name="filePath">c:\github\NDI-for-Android\specs\012-fix-prereq-preflight\quickstart.md