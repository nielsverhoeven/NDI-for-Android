# Feature 030 Preflight: Android Prerequisites

Date: 2026-04-19

Command:

```powershell
pwsh ./scripts/verify-android-prereqs.ps1
```

Result: PASS

Summary:

- java/javac/adb/cmake/ninja found on PATH
- sdkmanager/avdmanager/emulator found
- JAVA_HOME and ANDROID_SDK_ROOT configured
- NDI SDK detected
- Required Android SDK packages detected
