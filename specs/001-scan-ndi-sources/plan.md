# Implementation Plan: NDI Source Discovery and Viewing

**Branch**: `001-scan-ndi-sources` | **Date**: 2026-03-15 | **Spec**: `/specs/001-scan-ndi-sources/spec.md`
**Input**: Feature specification from `/specs/001-scan-ndi-sources/spec.md`

## Summary

Build an Android feature that discovers NDI sources on the local network,
allows user selection, and renders one selected source on phone/tablet screens.
Implementation will enforce MVVM + repository boundaries, foreground-only
discovery refresh (5-second interval), bounded reconnection (15 seconds), and
no location permission requirement. Planning includes prerequisite remediation
because Android toolchain commands are currently unavailable in the environment.

## Technical Context

**Language/Version**: Kotlin 1.9+ with Java 17 toolchain  
**Primary Dependencies**: AndroidX Navigation, Lifecycle/ViewModel, Coroutines/Flow, Room, Material Design 3 UI components, NDI 6 Android SDK native libraries  
**Storage**: Room database for offline-first persisted feature state (including last selected source identity)  
**Testing**: JUnit (unit tests), Espresso (UI flow tests), Android instrumentation test runner  
**Target Platform**: Android API 24+ (phones and tablets), target SDK 34+  
**Project Type**: Mobile app (feature-modular Android project)  
**Performance Goals**: Source discovery list refresh visible within 5 seconds in >=90% of runs; first frame displayed within 3 seconds in >=90% of runs  
**Constraints**: No location permission; no unjustified background discovery; auto-refresh only while source list is foreground; interruption auto-retry capped at 15 seconds; R8/ProGuard enabled for release  
**Scale/Scope**: Single active source playback at a time; one discovery/list screen and one viewer screen in initial release

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Design Gate Review

- MVVM gate: PASS - state/event logic planned in ViewModels, UI remains render-only.
- Navigation gate: PASS - single-activity, two destinations (source list -> viewer).
- Data gate: PASS - repository abstraction for discovery + selection persistence.
- TDD gate: PASS - test-first tasks planned (JUnit/Espresso before implementation).
- UX gate: PASS - Material 3 states for loading/empty/error/interruption.
- Battery gate: PASS - no background scanning; foreground-only interval refresh.
- Offline gate: PASS - Room persistence for last selected source identity and session metadata.
- Permission gate: PASS - explicit no-location-permission requirement.
- Modularity gate: PASS - feature module boundaries defined below.
- Release gate: PASS - release build verification with R8/ProGuard included in quickstart.
- Platform gate: PASS - API 24+ compatibility and target SDK 34+ fixed in plan.

### Post-Design Gate Review

- MVVM gate: PASS - contracts enforce ViewModel-owned state transitions.
- Navigation gate: PASS - navigation contract defines explicit route and arguments.
- Data gate: PASS - data model + contract maintain repository-only data access.
- TDD gate: PASS - quickstart includes failing-test-first flow and CI verification.
- UX gate: PASS - design artifacts include Material 3 interaction expectations.
- Battery gate: PASS - contract limits discovery lifecycle to foreground scope.
- Offline gate: PASS - data model captures persisted entities for offline-first behavior.
- Permission gate: PASS - no added dangerous permissions; validation step included.
- Modularity gate: PASS - module structure in project tree enforces separation.
- Release gate: PASS - quickstart includes minify/shrink release validation.
- Platform gate: PASS - prerequisite checks and build targets include API 24+/34+.

## Project Structure

### Documentation (this feature)

```text
specs/001-scan-ndi-sources/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── ndi-feature-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
app/
├── src/main/
│   ├── AndroidManifest.xml
│   └── java/.../app/navigation/
└── src/androidTest/

core/
├── model/
├── database/
└── testing/

feature/
└── ndi-browser/
    ├── data/
    ├── domain/
    ├── presentation/
    │   ├── source_list/
    │   └── viewer/
    ├── src/test/
    └── src/androidTest/

ndi/
└── sdk-bridge/
    ├── src/main/cpp/
    ├── src/main/jniLibs/
    └── src/test/
```

**Structure Decision**: Use a feature-modular Android layout with a dedicated
NDI SDK bridge module to isolate JNI/native dependencies from presentation and
domain code while preserving repository boundaries.

## Prerequisite Audit & Required Actions

Environment check result (current machine/session):

- Missing from PATH: `java`, `javac`, `adb`, `sdkmanager`, `avdmanager`,
  `emulator`, `gradle`, `cmake`, `ninja`.
- Missing environment variables: `JAVA_HOME`, `ANDROID_SDK_ROOT`, `ANDROID_HOME`.
- Installed evidence detected: `C:\Program Files\NDI\NDI 6 SDK (Android)`.

Required prerequisite actions to unblock implementation:

1. Install Android Studio (latest stable) with SDK Manager + command-line tools.
2. Install JDK 17 and set `JAVA_HOME`.
3. Set `ANDROID_SDK_ROOT` (and optional `ANDROID_HOME`) to SDK location.
4. Install SDK components: platform `android-34`, build-tools `34.x`,
   platform-tools, cmdline-tools, emulator, CMake, and NDK side-by-side.
5. Ensure PATH includes JDK bin and Android SDK tool directories.
6. Verify commands succeed: `java -version`, `adb version`,
   `sdkmanager --list`, and project `gradlew --version` once scaffolded.
7. Configure project-local NDI bridge to reference
   `C:\Program Files\NDI\NDI 6 SDK (Android)` and check required ABI libs are
   included in module packaging.
8. Add CI prerequisites check task so missing toolchain fails fast before build.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No constitution violations requiring justification.
