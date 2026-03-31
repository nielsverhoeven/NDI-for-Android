# NDI-for-Android Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-03-31

## Active Technologies
- Kotlin 1.9.24 with Android module source/jvm target 17; Gradle runtime verified with Android Studio JBR 21 + AGP 8.5.2, Gradle 8.7, AndroidX Navigation 2.7.7, Lifecycle 2.8.4, Coroutines 1.8.1, Room 2.6.1, Material 1.12.0, NDI 6 Android SDK native libraries (001-scan-ndi-sources)
- Room database for persisted selection state and recent viewer/session metadata (001-scan-ndi-sources)
- Kotlin 1.9.24 (Android modules target Java 17); Gradle runtime on Android Studio stable JBR 21 + AndroidX/Jetpack (Lifecycle, Navigation, Room), Material 3, Kotlin Coroutines, NDI 6 Android SDK via `ndi/sdk-bridge` (001-scan-ndi-sources)
- Room for user-critical continuity state and discovery/session metadata (001-scan-ndi-sources)
- Kotlin 1.9.24 (Android modules target Java 17); Gradle runtime on Android Studio stable JBR 21 + AndroidX/Jetpack (Lifecycle, Navigation, Room), Material 3, Kotlin Coroutines/Flow, NDI 6 Android SDK through `ndi/sdk-bridge` (002-stream-ndi-source)
- Room for persisted output configuration, continuity state, and recent output session metadata (002-stream-ndi-source)
- Kotlin 2.2.10 for Android modules with Java/Kotlin bytecode target 17, TypeScript 5.8.x for Playwright automation, PowerShell 5.1+ for Windows orchestration; Gradle wrapper verified on JDK 21.0.10 + AndroidX/Jetpack (Lifecycle, Navigation, Fragment, Activity, Room), Material components, Kotlin Coroutines/Flow, NDI 6 Android SDK via `ndi/sdk-bridge`, `@playwright/test` 1.53.x, Android `adb`/`emulator` CLI (002-stream-ndi-source)
- Room for persisted output configuration and continuity metadata; file-based Playwright reports/logcat artifacts for validation evidence (002-stream-ndi-source)
- PowerShell 5.1+ scripting for emulator provisioning, TCP socket relay, environment reset, and artifact collection; ADB CLI (`adb devices`, `adb shell logcat`); Bash 4.0+ for CI/CD helper scripts; Windows + Linux emulator toolchain support (010-dual-emulator-setup)
- TCP socket forwarding (raw TCP, no external relay libraries); Playwright 1.53+ for e2e test validation; JSON manifest generation for CI/CD artifact integration (010-dual-emulator-setup)
- Post-test artifact collection: logcat streaming, screen recording (MP4), diagnostic JSON, relay metrics, manifest generation with checksums (010-dual-emulator-setup)
- Android emulator (API 32-35), ADB tools, NDI SDK APK deployment, health check monitoring with automatic restart (010-dual-emulator-setup)

- Kotlin with the latest stable JDK/JBR supported by the current AGP/Gradle baseline + AndroidX Navigation, Lifecycle/ViewModel, Coroutines/Flow, Room, Material Design 3 UI components, NDI 6 Android SDK native libraries (001-scan-ndi-sources)

## Project Structure

```text
src/
tests/
```

## Commands

# Add commands for the repo-supported latest stable Android toolchain baseline

## Code Style

Kotlin with the repo-supported latest stable Android toolchain baseline: Follow standard conventions

## Recent Changes
- 002-stream-ndi-source: Added Kotlin 2.2.10 for Android modules with Java/Kotlin bytecode target 17, TypeScript 5.8.x for Playwright automation, PowerShell 5.1+ for Windows orchestration; Gradle wrapper verified on JDK 21.0.10 + AndroidX/Jetpack (Lifecycle, Navigation, Fragment, Activity, Room), Material components, Kotlin Coroutines/Flow, NDI 6 Android SDK via `ndi/sdk-bridge`, `@playwright/test` 1.53.x, Android `adb`/`emulator` CLI
- 002-stream-ndi-source: Added Kotlin 1.9.24 (Android modules target Java 17); Gradle runtime on Android Studio stable JBR 21 + AndroidX/Jetpack (Lifecycle, Navigation, Room), Material 3, Kotlin Coroutines/Flow, NDI 6 Android SDK through `ndi/sdk-bridge`
- 002-stream-ndi-source: Added Kotlin 1.9.24 (Android modules target Java 17); Gradle runtime on Android Studio stable JBR 21 + AndroidX/Jetpack (Lifecycle, Navigation, Room), Material 3, Kotlin Coroutines/Flow, NDI 6 Android SDK through `ndi/sdk-bridge`


<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
