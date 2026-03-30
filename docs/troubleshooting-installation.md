# Troubleshooting Installation Issues

**Feature**: 012-fix-prereq-preflight
**Date**: March 23, 2026

## Common Installation Problems

### License Acceptance Failures
**Symptoms**: Installation fails with "License acceptance failed"
**Cause**: SDK licenses not accepted or acceptance interrupted
**Solution**:
1. Run `sdkmanager --licenses` manually and accept all licenses
2. Ensure you have write permissions to the Android SDK directory
3. Check that `JAVA_HOME` is set correctly

### Network Connectivity Issues
**Symptoms**: "Network error" or timeout during download
**Cause**: Unstable internet connection or firewall blocking downloads
**Solution**:
1. Check internet connectivity
2. Verify firewall allows connections to dl.google.com
3. Try running the script again (automatic retry is built-in)
4. Use a VPN if corporate firewall is blocking

### Disk Space Issues
**Symptoms**: Installation fails with disk space errors
**Cause**: Insufficient space in Android SDK directory
**Solution**:
1. Free up disk space (Android SDK requires ~2-3GB)
2. Check available space: `Get-WmiObject Win32_LogicalDisk | Select-Object Size,FreeSpace`
3. Clean temporary files and caches

### Permission Issues
**Symptoms**: "Access denied" or permission errors
**Cause**: No write access to Android SDK directory
**Solution**:
1. Run PowerShell as Administrator
2. Check ownership of Android SDK folder
3. Ensure current user has full control permissions

### Timeout Issues
**Symptoms**: "Installation timed out" error
**Cause**: Slow network or large packages taking too long
**Solution**:
1. Increase timeout if needed (modify script parameter)
2. Check network speed
3. Try during off-peak hours

### SDK Manager Not Found
**Symptoms**: "sdkmanager not found" error
**Cause**: Android SDK not properly installed or PATH not set
**Solution**:
1. Verify `ANDROID_SDK_ROOT` environment variable
2. Add `ANDROID_SDK_ROOT\cmdline-tools\latest\bin` to PATH
3. Reinstall Android SDK if corrupted

## Debugging Steps

1. **Enable verbose logging**:
   ```powershell
   $VerbosePreference = "Continue"
   .\verify-android-prereqs.ps1
   ```

2. **Check SDK manager version**:
   ```bash
   sdkmanager --version
   ```

3. **List available packages**:
   ```bash
   sdkmanager --list
   ```

4. **Manual installation test**:
   ```bash
   sdkmanager "platform-tools"
   ```

## Getting Help

If issues persist:
1. Check the installation logs for specific error messages
2. Verify all prerequisites are met (see docs/android-prerequisites.md)
3. Test on a clean Windows environment
4. Report issues with full error logs and environment details