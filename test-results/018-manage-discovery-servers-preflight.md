# Phase 0: Environment Preflight — 018-manage-discovery-servers

**Date**: 2026-03-28  
**Status**: ✅ PASS (no blockers)

---

## T001 — Android Prerequisites (`scripts/verify-android-prereqs.ps1`)

```
Android prerequisite verification
[PASS] command:java            - Found on PATH
[PASS] command:javac           - Found on PATH
[PASS] command:adb             - Found on PATH
[PASS] command:sdkmanager      - Found on PATH
[PASS] command:avdmanager      - Found on PATH
[PASS] command:emulator        - Found on PATH
[PASS] command:cmake           - Found on PATH
[PASS] command:ninja           - Found on PATH
[PASS] build:gradle            - C:\githubrepos\NDI-for-Android\gradlew.bat
[PASS] env:JAVA_HOME           - C:\Program Files\Java\jdk-21.0.10
[PASS] env:ANDROID_SDK_ROOT    - C:\Android\Sdk
[PASS] sdk:NDI                 - C:\Program Files\NDI\NDI 6 SDK
[PASS] package:platform-tools  - C:\Android\Sdk\platform-tools
[PASS] package:platforms;android-34 - C:\Android\Sdk\platforms\android-34
[PASS] package:build-tools;34.0.0   - C:\Android\Sdk\build-tools\34.0.0
[PASS] package:emulator        - C:\Android\Sdk\emulator
```

**Outcome**: PASS — all 16 checks passed.

---

## T002 — Dual-Emulator Preflight (`scripts/verify-e2e-dual-emulator-prereqs.ps1 -AllowMissingNdiSdk`)

```json
{
  "operation": "verify-e2e-dual-emulator-prereqs",
  "status": "SUCCESS",
  "checks": [
    {"name": "adb",           "ok": true},
    {"name": "emulator",      "ok": true},
    {"name": "sdkmanager",    "ok": true},
    {"name": "ndi-sdk-artifact", "ok": true,
     "path": "ndi/sdk-bridge/build/outputs/aar/sdk-bridge-release.aar",
     "type": "aar", "warning": "library-artifact-only"}
  ],
  "errors": [],
  "warnings": []
}
```

**Outcome**: SUCCESS — dual-emulator toolchain ready. NDI SDK present as library artifact.

---

## T003 — Debug Artifact Build (`./gradlew.bat :app:assembleDebug`)

```
BUILD SUCCESSFUL in 7s
```

**Outcome**: PASS — debug APK assembles cleanly.

---

## T004 — Environment Blockers

**No blockers identified.** All prerequisite checks pass.

- JAVA_HOME: `C:\Program Files\Java\jdk-21.0.10` (JDK 21 — compatible with Gradle/AGP toolchain)
- ANDROID_SDK_ROOT: `C:\Android\Sdk`
- NDI SDK: `C:\Program Files\NDI\NDI 6 SDK`
- Gradle wrapper: present and functional

Unblock command (if rerunning): `pwsh -ExecutionPolicy Bypass -File scripts/verify-android-prereqs.ps1`
