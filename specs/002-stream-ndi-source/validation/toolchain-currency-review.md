# Toolchain Currency Review (TOOLCHAIN-001)

## Ownership

- Blocker ID: TOOLCHAIN-001
- Owner: TBD
- Target Resolution Date: TBD
- Target Resolution Cycle: TBD
- Status: OPEN

## Current Baseline Snapshot

- compileSdk: 34
- targetSdk: 34
- AGP: 8.5.2
- Gradle: 8.7
- Kotlin: 1.9.24
- JDK/JBR: Java 17 targets / Android Studio stable JBR 21
- AndroidX/Jetpack: See gradle/libs.versions.toml
- NDK/CMake: See local environment and build config
- NDI SDK: 6.x bridge compatibility under validation

## Review Log

| Date | Reviewer | Findings | Action |
|---|---|---|---|
| TBD | TBD | Initial tracker created | Pending detailed review |
| 2026-03-16 | Copilot | Local validation used JDK 21 (`C:\Program Files\Java\jdk-21.0.10`); US1-US3 unit/data tests and debug assemble passed | Keep TOOLCHAIN-001 OPEN until dual-emulator and release validation complete |
