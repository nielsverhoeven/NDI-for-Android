# Quickstart: NDI Source Discovery and Viewing

## 1. Prerequisites (Required Before Coding)

Current audit summary from this workspace session:
- Android toolchain commands are missing from PATH (`java`, `adb`, `sdkmanager`, etc.).
- `JAVA_HOME`, `ANDROID_SDK_ROOT`, and `ANDROID_HOME` are not set.
- NDI Android SDK appears installed at `C:\Program Files\NDI\NDI 6 SDK (Android)`.

### 1.1 Install core tooling
1. Install Android Studio (latest stable) with SDK Manager and command-line tools.
2. Install JDK 17.
3. Configure environment variables:
   - `JAVA_HOME=<path-to-jdk-17>`
   - `ANDROID_SDK_ROOT=<path-to-android-sdk>`
   - Optional compatibility: `ANDROID_HOME=<path-to-android-sdk>`
4. Add to PATH:
   - `%JAVA_HOME%\bin`
   - `%ANDROID_SDK_ROOT%\platform-tools`
   - `%ANDROID_SDK_ROOT%\cmdline-tools\latest\bin`
   - `%ANDROID_SDK_ROOT%\emulator`

### 1.2 Install SDK packages
Install these components via SDK Manager:
- Android SDK Platform 34
- Android SDK Build-Tools 34.x
- Android SDK Platform-Tools
- Android SDK Command-line Tools (latest)
- Android Emulator
- NDK (side-by-side, latest stable supported by chosen AGP)
- CMake

### 1.3 Verify toolchain
Run and confirm success:
- `java -version`
- `adb version`
- `sdkmanager --list`

## 2. Project Setup

1. Create/scaffold Android project with minSdk 24 and targetSdk 34.
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
2. Write failing Espresso tests for:
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

- `./gradlew test`
- `./gradlew connectedAndroidTest`
- `./gradlew :app:assembleRelease`

Expected outcomes:
- Unit and UI tests pass.
- Release build succeeds with shrinking/optimization enabled.
- No unauthorized permission additions in manifest.
