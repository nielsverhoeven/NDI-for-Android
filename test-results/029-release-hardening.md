# 029 Release Hardening

Date: 2026-04-07
Feature: 029-ndi-server-compatibility

## Command
```powershell
$env:JAVA_HOME='C:\Program Files\Android\Android Studio\jbr'
$env:ANDROID_HOME='C:\Users\Niels\AppData\Local\Android\Sdk'
$env:ANDROID_SDK_ROOT='C:\Users\Niels\AppData\Local\Android\Sdk'
.\gradlew.bat :app:verifyReleaseHardening --console=plain
```

## Result
- PASS: BUILD SUCCESSFUL.
- `verifyReleaseHardening` completed successfully.
- Gradle reported deprecation warnings for future Gradle 10 compatibility, but no release hardening failure.
