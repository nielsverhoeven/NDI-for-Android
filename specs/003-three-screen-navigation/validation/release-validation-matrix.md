# Release Validation Matrix: Three-Screen Navigation

**Feature**: Spec 003 – Three-Screen NDI Navigation  
**Date**: 2026-03-17

## Release Hardening Gates

| Gate | Command | Status |
|------|---------|--------|
| `verifyReleaseHardening` | `./gradlew verifyReleaseHardening` | NOT_RUN (pending build env) |
| Release assembly | `./gradlew :app:assembleRelease` | NOT_RUN (NDI SDK required) |
| isMinifyEnabled | `app/build.gradle.kts` | ✓ true |
| isShrinkResources | `app/build.gradle.kts` | ✓ true |

## Phone Matrix

| Device | API | Bottom Nav | Home Entry | Stream | View | Deep Links |
|--------|-----|-----------|------------|--------|------|-----------|
| Pixel 4a (360dp) | 30 | ✓ Unit | ✓ Unit | ✓ Unit | ✓ Unit | ✓ Unit |
| Pixel 6 (411dp) | 33 | ✓ Unit | ✓ Unit | ✓ Unit | ✓ Unit | ✓ Unit |

## Tablet Matrix

| Device | API | Nav Rail | Home Entry | Stream | View | Deep Links |
|--------|-----|----------|------------|--------|------|-----------|
| Pixel Tablet (800dp) | 33 | ✓ Unit | ✓ Unit | ✓ Unit | ✓ Unit | ✓ Unit |

## Notes

- Device-connected instrumentation runs pending emulator/device availability.
- All behavioral constraints are covered by unit tests.
- Release assembly verification pending NDI SDK availability in CI environment.

