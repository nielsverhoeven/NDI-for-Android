# Android Prerequisites

Run the local verification before opening the project in Android Studio or invoking Gradle:

```powershell
pwsh ./scripts/verify-android-prereqs.ps1
```

Required machine state:

- `JAVA_HOME` points to JDK 21 (recommended) or Android Studio stable JBR,
  aligned with the repo's current AGP/Gradle baseline
- Android SDK configured through `ANDROID_SDK_ROOT` or `ANDROID_HOME`
- CLI tools available on `PATH`: `java`, `javac`, `adb`, `sdkmanager`, `avdmanager`, `emulator`, `cmake`, `ninja`
- `gradlew.bat` and the Gradle wrapper files exist in the repo root; use a
  global `gradle` install only when intentionally regenerating the wrapper
- Android SDK packages installed to match the versions declared in the repo's
  build files: `platform-tools`, the current compileSdk/targetSdk platform,
  compatible build-tools, command-line tools, emulator, NDK, and CMake
- NDI Android SDK installed locally at `C:\Program Files\NDI\NDI 6 SDK (Android)` or referenced via `NDI_SDK_DIR` / `ndi.sdk.dir`

Setup steps:

1. Install Android Studio stable.
2. Set `JAVA_HOME` to JDK 21 (recommended) or Android Studio stable JBR
  supported by the repo's current AGP/Gradle pair.
3. Install the Android SDK platform, build-tools, platform-tools, emulator,
  command-line tools, NDK, and CMake versions required by the current build files.
4. Verify the checked-in Gradle wrapper with `./gradlew --version` or
  `.\gradlew.bat --version`. Regenerate the wrapper only when intentionally
  upgrading Gradle.
5. Copy `local.properties.example` to `local.properties` and adjust `sdk.dir` and `ndi.sdk.dir`.
6. Re-run the verification script and resolve any failed checks before building.

CI usage:

```powershell
pwsh ./scripts/verify-android-prereqs.ps1 -CiMode -AllowMissingNdiSdk
```

The CI mode skips the proprietary NDI SDK requirement but still fails if the Android/JDK toolchain is incomplete.
