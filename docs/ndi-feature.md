# NDI Feature Implementation

Implemented feature areas:

- NDI source discovery with loading, empty, success, and failure UI states
- Foreground-only 5 second refresh scheduling plus manual refresh
- Source selection persistence with highlight-on-relaunch and no autoplay
- Viewer playback flow with phone/tablet adaptive layout behavior
- Interruption handling with bounded reconnect attempts, retry, and return-to-list actions

Validation status:

- Android/JDK/SDK prerequisite checks are automated in `scripts/verify-android-prereqs.ps1`
- The Gradle wrapper is checked in at the repo root and MUST be validated with `gradlew(.bat) --version` after toolchain upgrades
- Build logic uses JDK 21 toolchains where available while keeping Java/Kotlin bytecode targets at 17 for Android compatibility
- Release hardening is defined in the `verifyReleaseHardening` Gradle task in `app/build.gradle.kts`
- Build and validation work MUST use the latest stable Android toolchain baseline compatible with the current AGP/Gradle/NDI SDK combination

Recommended validation commands:

1. `./gradlew --version`
2. `./gradlew verifyReleaseHardening`
3. `./gradlew test connectedAndroidTest :app:assembleRelease`
