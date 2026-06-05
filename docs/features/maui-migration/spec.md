# Feature: Rewrite NDI-for-Android as .NET MAUI

## Overview
Migrate the Android NDI application architecture to a .NET MAUI implementation while preserving existing user-visible behavior and operational reliability. The migrated app must keep discovery, viewing, output, settings, diagnostics, and persistence behavior equivalent to the legacy app, with Android-specific concerns implemented through platform services under the MAUI architecture defined by the project constitution.

## User Stories
- As a user, I want NDI source discovery to work the same way as before so that I can find available sources quickly.
- As a user, I want viewing and output flows to remain stable so that I can monitor or stream without interruptions.
- As a user, I want settings and server configuration to persist so that I do not need to reconfigure on every restart.
- As a maintainer, I want CI to validate build and test gates on every PR so that regressions are caught before merge.

## Functional Requirements
1. The application shall provide NDI source discovery with persisted server configuration support.
2. The application shall provide NDI viewer playback and NDI output/screen-share workflows through MAUI navigation routes.
3. The application shall preserve settings management, including discovery server values and developer diagnostics toggles.
4. The application shall persist configured data in SQLite and restore it after app restart.
5. The application shall provide primary navigation flows for Home/Sources, Viewer, Output, and Settings destinations.
6. The application shall implement Android-specific capabilities (for example MediaProjection and foreground execution requirements) via platform-specific services.
7. The application shall isolate native NDI interop behind bridge interfaces so that ViewModels and Views do not consume native SDK types directly.
8. The project shall provide automated build and test execution in CI for pull requests.

## Non-Functional Requirements
- Performance: viewer and output reconnection behavior must remain within existing operational expectations (target: recovery within 15 seconds after transient loss).
- Reliability: navigation transitions must not orphan active NDI sessions.
- Maintainability: layered architecture and DI boundaries from `docs/constitution.md` must be enforced.
- Security and safety: no secrets are stored in source; Android manifest permissions must follow constitution requirements.
- Compatibility: app targets Android API levels defined by constitution (`minSdk 26`, `targetSdk 35`).

## Success Criteria
1. A MAUI Android build (`net10.0-android`) succeeds in CI and local build validation.
2. Discovery, viewer, output, and settings user flows are executable end-to-end in MAUI.
3. Settings and discovery server values persist across app restart.
4. Automated tests cover repositories/ViewModels and core navigation behavior with passing `dotnet test`.
5. Android emulator workflow installs and runs the APK successfully in CI.
6. Native interop calls are routed through bridge interfaces and do not leak native SDK types into UI layers.
7. Reconnect and lifecycle behavior is validated with evidence that operational recovery target is met.
8. Documentation and runbook artifacts describe migration architecture, test evidence, and operational constraints.

## Out of Scope
- Introducing new end-user features beyond existing Kotlin-app behavior.
- Expanding platform support beyond Android for this migration scope.
- Replacing the NDI SDK itself or changing transport semantics.

## Assumptions
- The project constitution is the authoritative technical baseline for migration decisions.
- NDI integration uses P/Invoke against `libndi.so` per constitution.
- Existing migration task issues under `docs/features/maui-migration/tasks.md` remain the execution breakdown source.
- Existing CI workflows remain the enforcement point for build/test merge gates.

## Open Questions
- None.
