# Toolchain Blockers

## TOOLCHAIN-001

- Status: OPEN
- Owner: Mobile Maintainers
- Affected components:
  - app/build.gradle.kts
  - core/database/build.gradle.kts
  - feature/ndi-browser/domain/build.gradle.kts
  - feature/ndi-browser/data/build.gradle.kts
  - feature/ndi-browser/presentation/build.gradle.kts
  - ndi/sdk-bridge/build.gradle.kts
  - scripts/verify-android-prereqs.ps1
  - .github/workflows/android-ci.yml
  - docs/android-prerequisites.md
- Current baseline:
  - compileSdk 34 / targetSdk 34
  - Java source/jvm target 17 in Android modules
  - AGP 8.5.2, Gradle 8.7, Kotlin 1.9.24
  - Android Studio stable JBR 21 used for Gradle runtime
- Target baseline:
  - Latest stable compatible Android API/JDK baseline validated against NDI SDK compatibility constraints
- Blocker reason:
  - NDI SDK compatibility against a newer stable Android API/JDK baseline is not yet validated.
- Target resolution date:
  - 2026-06-30
- Target resolution cycle:
  - 2026-Q2 maintenance cycle
- Exit criteria:
  - Compatibility verification completed for NDI SDK on target baseline.
  - Build files, CI, prerequisite docs, and validation commands updated in one change.
  - Discovery, selection, viewer, interruption-recovery, and release-validation flows pass on representative phone and tablet devices.
