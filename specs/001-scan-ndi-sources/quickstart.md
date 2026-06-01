# Quickstart: NDI Source Discovery and Viewing

## 1. Prerequisites (Required Before Coding)

Current audit summary from this workspace session:

- `pwsh ./scripts/verify-android-prereqs.ps1` passed on 2026-03-15.
- The checked-in Gradle wrapper is available and Gradle 8.7 execution has been
   verified with Android Studio stable JBR 21.
- NDI Android SDK is available locally.
- Repository Android modules still declare compile/target SDK 34 and Java 17
   source/jvm targets; this remains tracked under blocker `TOOLCHAIN-001` until
   the latest stable compatible Android baseline is validated.

### 1.1 Install core tooling

1. Install Android Studio (latest stable) with SDK Manager and command-line tools.
2. Use Android Studio stable JBR or install the latest stable JDK supported by
   the repo's current AGP/Gradle pair.
3. Configure environment variables:
   - `JAVA_HOME=<path-to-supported-jdk-or-jbr>`
   - `ANDROID_SDK_ROOT=<path-to-android-sdk>`
   - Optional compatibility: `ANDROID_HOME=<path-to-android-sdk>`
4. Add to PATH:
   - `%JAVA_HOME%\bin`
   - `%ANDROID_SDK_ROOT%\platform-tools`
   - `%ANDROID_SDK_ROOT%\cmdline-tools\latest\bin`
   - `%ANDROID_SDK_ROOT%\emulator`

### 1.2 Install SDK packages

Install these components via SDK Manager:

- Android SDK Platform matching the repo's current compile/target SDK baseline
- Android SDK Build-Tools compatible with the repo's current AGP/Gradle baseline
- Android SDK Platform-Tools
- Android SDK Command-line Tools (latest)
- Android Emulator
- NDK (side-by-side, latest stable supported by chosen AGP)
- CMake

Current repo-supported baseline before blocker resolution:

- AGP 8.5.2
- Gradle 8.7
- Kotlin 1.9.24
- compileSdk 34 / targetSdk 34
- Java source/jvm target 17 in Android modules
- Android Studio stable JBR 21 for Gradle execution

### 1.3 Verify toolchain

Run and confirm success:

- `java -version`
- `adb version`
- `sdkmanager --list`

## 2. Project Setup

1. Create/scaffold Android project with minSdk 24 and the latest stable
   compatible Android baseline once blocker `TOOLCHAIN-001` is resolved. Until
   then, preserve the checked-in baseline and document the blocker in planning
   and release-readiness checks.
2. Enable release shrinking/optimization (R8/ProGuard) in release build type.
3. Create modules:
   - `app`
   - `core:model`, `core:database`, `core:testing`
   - `feature:ndi-browser`
   - `ndi:sdk-bridge`
4. Configure single-activity navigation in `app` module.

## 3. NDI SDK Wiring

1. Point local build config to NDI Android SDK directory:
   - `C:\Program Files\NDI\NDI 6 SDK (Android)`
2. Add NDI native libs to `ndi:sdk-bridge` for required ABIs (`arm64-v8a`, `armeabi-v7a`, optional x86/x86_64 for emulator/testing).
3. Add JNI bridge layer in `ndi:sdk-bridge` and expose repository-safe interfaces to `feature:ndi-browser`.
4. Validate native loading on app startup path used by the feature.

## 4. TDD-First Implementation Flow

1. Write failing unit tests for:
   - discovery state transitions
   - source identity handling
   - selection persistence
   - interruption retry window behavior
2. Write failing Playwright end-to-end tests for:
   - source list -> viewer navigation
   - no-autoplay on launch with highlighted previous source
   - recovery actions after unresolved interruption
3. Implement minimal code to pass tests.
4. Refactor while keeping tests green.

## 5. Feature Behavior Checks

1. Discovery auto-refresh runs every 5 seconds only while source list is foreground.
2. Manual refresh is available.
3. Source identity is keyed by stable endpoint ID.
4. App launch highlights previous source but does not autoplay.
5. Interruption auto-retries up to 15 seconds, then shows retry/reselect actions.
6. No location permission is requested.

## 6. Validation Commands (after scaffold)

- `pwsh ./scripts/verify-android-prereqs.ps1`
- `./gradlew --version`
- `./gradlew verifyReleaseHardening`
- `./gradlew test`
- `./gradlew connectedAndroidTest`
- `./gradlew :app:assembleRelease`

Expected outcomes:

- Unit and UI tests pass.
- Release build succeeds with shrinking/optimization enabled.
- No unauthorized permission additions in manifest.
- Any deferred toolchain uplift remains documented under `TOOLCHAIN-001` with
   owner, affected components, and target resolution cycle.
