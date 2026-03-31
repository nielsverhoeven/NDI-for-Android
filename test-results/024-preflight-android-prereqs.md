# 024 Preflight - Android Prerequisites (T001)

- Command: `pwsh ./scripts/verify-android-prereqs.ps1 -CiMode -AllowMissingNdiSdk`
- Date: 2026-03-31
- Status: PASS

## Key Checks

- `java`: PASS
- `javac`: PASS
- `adb`: PASS
- `sdkmanager`: PASS
- `avdmanager`: PASS
- `emulator`: PASS
- `cmake`: PASS
- `ninja`: PASS
- `gradle wrapper`: PASS
- `JAVA_HOME`: PASS (`C:\Program Files\Java\jdk-21.0.10`)
- `ANDROID_SDK_ROOT`: PASS (`C:\Android\Sdk`)
- `NDI SDK`: PASS (`C:\Program Files\NDI\NDI 6 SDK`)
- Required Android packages: PASS (`platform-tools`, `platforms;android-34`, `build-tools;34.0.0`, `emulator`)

## Notes

- No blocking issues detected.
- Environment is ready for e2e validation prerequisites.
