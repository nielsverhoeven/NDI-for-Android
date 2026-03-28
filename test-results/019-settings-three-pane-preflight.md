# 019 Settings Three-Pane Preflight

Date: 2026-03-28

## T001 scripts/verify-android-prereqs.ps1

Status: PASS

```text
Android prerequisite verification
[PASS] command:java - Found on PATH
[PASS] command:javac - Found on PATH
[PASS] command:adb - Found on PATH
[PASS] command:sdkmanager - Found on PATH
[PASS] command:avdmanager - Found on PATH
[PASS] command:emulator - Found on PATH
[PASS] command:cmake - Found on PATH
[PASS] command:ninja - Found on PATH
[PASS] build:gradle - C:\githubrepos\NDI-for-Android\scripts\..\gradlew.bat
[PASS] env:JAVA_HOME - C:\Program Files\Java\jdk-21.0.10
[PASS] env:ANDROID_SDK_ROOT - C:\Android\Sdk
[PASS] sdk:NDI - C:\Program Files\NDI\NDI 6 SDK
[PASS] package:platform-tools - C:\Android\Sdk\platform-tools
[PASS] package:platforms;android-34 - C:\Android\Sdk\platforms\\android-34
[PASS] package:build-tools;34.0.0 - C:\Android\Sdk\build-tools\\34.0.0
[PASS] package:emulator - C:\Android\Sdk\emulator
```

## T002 scripts/verify-e2e-dual-emulator-prereqs.ps1 -AllowMissingNdiSdk

Status: PASS

```json
{
  "operation": "verify-e2e-dual-emulator-prereqs",
  "status": "SUCCESS",
  "data": {
    "checks": [
      { "name": "adb", "ok": true },
      { "name": "emulator", "ok": true },
      { "name": "sdkmanager", "ok": true },
      {
        "name": "ndi-sdk-artifact",
        "ok": true,
        "path": "C:\\githubrepos\\NDI-for-Android\\ndi\\sdk-bridge\\build\\outputs\\aar\\sdk-bridge-release.aar",
        "type": "aar",
        "warning": "library-artifact-only"
      }
    ]
  },
  "errors": [],
  "warnings": []
}
```

## T003 ./gradlew.bat :app:assembleDebug

Status: PASS

```text
BUILD SUCCESSFUL in 29s
200 actionable tasks: 18 executed, 182 up-to-date
APK exported to C:\githubrepos\NDI-for-Android\exports\ndi-for-android-0.6.0.apk
```

## T004 Blocker classification

No environment blockers were observed. Gate classification: PASS.

Unblock command: Not required.
