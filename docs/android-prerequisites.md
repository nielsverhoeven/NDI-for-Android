# Android Prerequisites

Run the local verification before opening the project in Android Studio or invoking Gradle:

```powershell
pwsh ./scripts/verify-android-prereqs.ps1
```

Required machine state:

- JDK 17 installed and exposed through `JAVA_HOME`
- Android SDK configured through `ANDROID_SDK_ROOT` or `ANDROID_HOME`
- CLI tools available on `PATH`: `java`, `javac`, `adb`, `sdkmanager`, `avdmanager`, `emulator`, `cmake`, `ninja`
- Android SDK packages installed: `platform-tools`, `platforms;android-34`, `build-tools;34.0.0`
- NDI Android SDK installed locally at `C:\Program Files\NDI\NDI 6 SDK (Android)` or referenced via `NDI_SDK_DIR` / `ndi.sdk.dir`

Setup steps:

1. Install Android Studio stable.
2. Install Java SE Development kit 25.
3. Install Android SDK Platform, Build-Tools , Platform-Tools, Emulator, CMake, NDK, and cmdline-tools.
4. Copy `local.properties.example` to `local.properties` and adjust `sdk.dir` and `ndi.sdk.dir`.
5. Re-run the verification script and resolve any failed checks before building.

CI usage:

```powershell
pwsh ./scripts/verify-android-prereqs.ps1 -CiMode -AllowMissingNdiSdk
```

The CI mode skips the proprietary NDI SDK requirement but still fails if the Android/JDK toolchain is incomplete.
