<!-- Last updated: 2026-03-20 -->

# Developer Documentation Index

This index is the entry point for day-to-day development on NDI-for-Android, including the completed Settings Menu feature (spec 006).

## Table of Contents

1. [Quick Start](#1-quick-start)
2. [Module Layout](#2-module-layout)
3. [Build and Test Commands](#3-build-and-test-commands)
4. [Troubleshooting](#4-troubleshooting)
5. [Related Docs](#5-related-docs)

## 1. Quick Start

1. Verify prerequisites.

```powershell
pwsh ./scripts/verify-android-prereqs.ps1
./gradlew.bat --version
```

2. Build debug artifacts.

```powershell
./gradlew.bat :app:assembleDebug
```

3. Run unit tests.

```powershell
./gradlew.bat test
```

4. Run instrumentation tests (device/emulator required).

```powershell
./gradlew.bat connectedAndroidTest
```

5. Run release hardening gate.

```powershell
./gradlew.bat verifyReleaseHardening :app:assembleRelease
```

## 2. Module Layout

| Module | Purpose |
|---|---|
| `:app` | App composition root. Owns `AppGraph` wiring, nav graph, and activity-level orchestration. |
| `:core:model` | Shared domain/data models (`NdiSettingsSnapshot`, `NdiDiscoveryEndpoint`, telemetry event constants). |
| `:core:database` | Room entities/DAOs and migrations, including `settings_preference`. |
| `:core:testing` | Shared testing helpers used across modules. |
| `:feature:ndi-browser:domain` | Repository interfaces and contracts used by feature presentation/data layers. |
| `:feature:ndi-browser:data` | Repository implementations for discovery, viewer, output, settings, and diagnostics. |
| `:feature:ndi-browser:presentation` | Fragments/screens/ViewModels for Source List, Viewer, Output, Settings, and overlay rendering. |
| `:ndi:sdk-bridge` | Native bridge boundary for NDI discovery/viewer/output operations. |

## 3. Build and Test Commands

### 3.1 Core Build

```powershell
./gradlew.bat :app:assembleDebug
./gradlew.bat :app:assembleRelease
```

### 3.2 Unit Tests by Module Scope

```powershell
./gradlew.bat :core:model:test
./gradlew.bat :feature:ndi-browser:data:test
./gradlew.bat :feature:ndi-browser:presentation:testDebugUnitTest
```

### 3.3 Instrumentation Tests

```powershell
./gradlew.bat :feature:ndi-browser:presentation:connectedDebugAndroidTest
./gradlew.bat :app:connectedDebugAndroidTest
```

### 3.4 E2E Dual-Emulator Validation

Use the launcher script documented in `testing/e2e/README.md`.

```powershell
powershell -ExecutionPolicy Bypass -File .\testing\e2e\scripts\run-dual-emulator-e2e.ps1 -EmulatorASerial emulator-5554 -EmulatorBSerial emulator-5556
```

## 4. Troubleshooting

### 4.1 Prerequisite Script Fails

- Re-run with explicit CI mode options when needed:

```powershell
pwsh ./scripts/verify-android-prereqs.ps1 -CiMode -AllowMissingNdiSdk
```

- Ensure PATH contains: `java`, `javac`, `adb`, `sdkmanager`, `avdmanager`, `emulator`, `cmake`, `ninja`.

### 4.2 Gradle Cache or Dependency Corruption

```powershell
./gradlew.bat --stop
./gradlew.bat clean
./gradlew.bat --refresh-dependencies :app:assembleDebug
```

If needed, clear the local wrapper/cache directory manually and rebuild.

### 4.3 Emulator/E2E Issues

- Keep both emulator windows visible; hidden/minimized windows can invalidate screenshot-based checks.
- Verify both devices are online:

```powershell
adb devices
```

- Use preflight mode first:

```powershell
powershell -ExecutionPolicy Bypass -File .\testing\e2e\scripts\run-dual-emulator-e2e.ps1 -EmulatorASerial emulator-5554 -EmulatorBSerial emulator-5556 -PreflightOnly
```

## 5. Related Docs

- Feature implementation details: `docs/ndi-feature.md`
- Architecture and flow diagrams: `docs/architecture.md`
- Testing guide: `docs/testing.md`
- 006 release and operator notes: `docs/006-settings-menu-release-notes.md`
- E2E harness guide: `testing/e2e/README.md`
- 006 spec: `specs/006-settings-menu/spec.md`
- 006 manual test quickstart: `specs/006-settings-menu/quickstart.md`
