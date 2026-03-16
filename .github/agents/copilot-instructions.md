# NDI-for-Android Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-03-15

## Active Technologies
- Kotlin 1.9.24 with Android module source/jvm target 17; Gradle runtime verified with Android Studio JBR 21 + AGP 8.5.2, Gradle 8.7, AndroidX Navigation 2.7.7, Lifecycle 2.8.4, Coroutines 1.8.1, Room 2.6.1, Material 1.12.0, NDI 6 Android SDK native libraries (001-scan-ndi-sources)
- Room database for persisted selection state and recent viewer/session metadata (001-scan-ndi-sources)
- Kotlin 1.9.24 (Android modules target Java 17); Gradle runtime on Android Studio stable JBR 21 + AndroidX/Jetpack (Lifecycle, Navigation, Room), Material 3, Kotlin Coroutines, NDI 6 Android SDK via `ndi/sdk-bridge` (001-scan-ndi-sources)
- Room for user-critical continuity state and discovery/session metadata (001-scan-ndi-sources)

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
- 001-scan-ndi-sources: Added Kotlin 1.9.24 (Android modules target Java 17); Gradle runtime on Android Studio stable JBR 21 + AndroidX/Jetpack (Lifecycle, Navigation, Room), Material 3, Kotlin Coroutines, NDI 6 Android SDK via `ndi/sdk-bridge`
- 001-scan-ndi-sources: Added Kotlin 1.9.24 with Android module source/jvm target 17; Gradle runtime verified with Android Studio JBR 21 + AGP 8.5.2, Gradle 8.7, AndroidX Navigation 2.7.7, Lifecycle 2.8.4, Coroutines 1.8.1, Room 2.6.1, Material 1.12.0, NDI 6 Android SDK native libraries

- 001-scan-ndi-sources: Added Kotlin with the repo-supported latest stable Android toolchain baseline + AndroidX Navigation, Lifecycle/ViewModel, Coroutines/Flow, Room, Material Design 3 UI components, NDI 6 Android SDK native libraries

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
