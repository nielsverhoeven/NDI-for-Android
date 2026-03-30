# Research: Fix Prereq Preflight

**Feature**: 012-fix-prereq-preflight
**Date**: March 23, 2026
**Phase**: 0 (Research & Technical Feasibility)

## Current State Analysis

### Existing Prerequisites Verification
The `scripts/verify-android-prereqs.ps1` script currently performs verification-only checks:
- Command availability on PATH (java, javac, adb, sdkmanager, etc.)
- Environment variables (JAVA_HOME, ANDROID_SDK_ROOT)
- Gradle wrapper presence
- NDI SDK location
- Android SDK package installation (platform-tools, platforms;android-34, build-tools;34.0.0)

### CI Workflow Analysis
The `.github/workflows/android-ci.yml` preflight job:
1. Checks out code
2. Sets up JDK 21 using `actions/setup-java`
3. Sets `ANDROID_SDK_ROOT` to `$ANDROID_HOME`
4. Runs verification script with `-CiMode -AllowMissingNdiSdk`

**Key Finding**: CI provides JDK and Android SDK root, but does not pre-install Android SDK packages. The verification script detects missing packages but does not install them.

### Common CI Failure Patterns
Based on typical Android CI setups, missing components are usually:
- Android SDK packages (platforms, build-tools, platform-tools)
- Occasionally command-line tools updates
- NDK/Cmake (though not checked in current script)

## Implementation Options

### Option 1: Script-Based Installation (RECOMMENDED)
- Modify `verify-android-prereqs.ps1` to install missing packages using `sdkmanager`
- Accept SDK licenses automatically
- Retry logic for network failures
- Maintain verification-first approach

**Pros**: Self-contained, works with existing CI, minimal workflow changes
**Cons**: Adds complexity to script, potential time impact

### Option 2: CI Workflow Pre-Installation
- Add GitHub Actions steps before verification to install Android SDK components
- Use `android-sdk-tools/setup-android-sdk` or similar actions
- Keep verification script as-is

**Pros**: Clean separation, easier testing
**Cons**: Workflow changes required, potential duplication

### Option 3: Pre-Built SDK Images
- Use custom Docker images or GitHub-hosted runners with pre-installed SDK
- Minimal runtime installation

**Pros**: Fast, reliable
**Cons**: Requires infrastructure changes, less flexible

### Option 4: Hybrid Approach
- CI installs base packages via workflow
- Script handles dynamic/missing components
- Caching for faster subsequent runs

**Pros**: Best of both, with caching benefits
**Cons**: More complex setup

## Risk Assessment

### High Priority Risks
1. **Installation Time**: SDK downloads could exceed 5-minute target
   - Mitigation: Parallel downloads, caching, selective installation

2. **Network Failures**: Unreliable downloads in CI
   - Mitigation: Retry logic, timeout handling, fallback options

3. **License Acceptance**: SDK licenses must be accepted non-interactively
   - Mitigation: Pre-accept all licenses, document requirements

4. **Disk Space**: Additional storage requirements
   - Mitigation: Clean up temporary files, monitor usage

### Medium Priority Risks
5. **Compatibility**: Changes might affect local development workflow
   - Mitigation: Add CI-only flags, maintain backward compatibility

6. **Version Conflicts**: Installing packages might conflict with CI environment
   - Mitigation: Use exact versions, test thoroughly

## Technical Feasibility

### Required Capabilities
- PowerShell execution of sdkmanager commands
- License acceptance automation
- Progress monitoring and timeout handling
- Error recovery and logging

### Dependencies
- Android SDK command-line tools must be available
- Network access to Google Maven repositories
- Sufficient disk space (estimated 2-3GB for full SDK)

### Success Criteria Validation
- Installation completes within 5 minutes on standard CI runners
- All required packages install successfully
- No breaking changes to existing functionality
- Clear error reporting for troubleshooting

## Recommendation

**Selected Approach**: Option 1 (Script-Based Installation)

**Rationale**: 
- Minimal changes to CI infrastructure
- Self-contained solution that works with existing setup
- Easier maintenance and testing
- Aligns with "optimize the preflight action" requirement

**Implementation Notes**:
- Add installation functions to existing script
- Use `--package` parameter for sdkmanager to install specific packages
- Accept licenses using `yes | sdkmanager --licenses`
- Add timeout and retry logic
- Maintain existing verification logic</content>
<parameter name="filePath">c:\github\NDI-for-Android\specs\012-fix-prereq-preflight\research.md