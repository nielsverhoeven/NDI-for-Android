# 027 Preflight: Android Prerequisites

- Command: `pwsh ./scripts/verify-android-prereqs.ps1`
- Status: PASS
- Exit: 0

## Key checks observed

- Required CLI commands on PATH: PASS (`java`, `javac`, `adb`, `sdkmanager`, `avdmanager`, `emulator`, `cmake`, `ninja`)
- Environment: PASS (`JAVA_HOME`, `ANDROID_SDK_ROOT`)
- Android SDK packages: PASS (`platform-tools`, `platforms;android-34`, `build-tools;34.0.0`, `emulator`)
- NDI SDK path check: PASS

## Classification

- Result type: `pass`
- Blocker: none
