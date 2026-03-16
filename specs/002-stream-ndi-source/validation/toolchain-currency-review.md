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
- minSdk: 24
- AGP: 9.0.0
- Gradle: 9.2.1 (verified with `./gradlew.bat --version` on 2026-03-16)
- Kotlin: 2.2.10 plugin in `gradle/libs.versions.toml`; Gradle runtime reports Kotlin 2.2.20
- JDK/JBR: JDK 21 toolchain/runtime with Java 17 bytecode targets
- AndroidX/Jetpack: See gradle/libs.versions.toml
- NDK/CMake: See local environment and build config
- NDI SDK: 6.x bridge compatibility under validation
- Validation gap: dual-emulator Android-device Playwright automation and final release evidence remain incomplete

## Review Log

| Date | Reviewer | Findings | Action |
|---|---|---|---|
| TBD | TBD | Initial tracker created | Pending detailed review |
| 2026-03-16 | Copilot | Verified wrapper/runtime at Gradle 9.2.1 on JDK 21.0.10; branch build files declare AGP 9.0.0 / Kotlin 2.2.10 / compileSdk 34 / targetSdk 34 / minSdk 24 | Keep TOOLCHAIN-001 OPEN until dual-emulator Android-device automation and release validation evidence are complete |
