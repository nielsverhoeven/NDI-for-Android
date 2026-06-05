# Technical Plan: Rewrite NDI-for-Android as .NET MAUI

## Current Migration Baseline (Partially Completed)
This migration is not starting from zero. The repository already contains substantial MAUI migration work in `src/MauiApp`, `src/Core`, `tests/MauiApp.Tests`, and `tests/MauiApp.UITests`.

Planning and implementation must treat the existing MAUI codebase as a partial migration baseline and focus on parity gaps, architectural hardening, and correctness verification rather than re-scaffolding from scratch.

## Historical Source-of-Truth Requirement
For any behavior where the intended implementation is ambiguous in the current MAUI code, use historical commits and legacy Kotlin-era sources as canonical references for parity decisions.

Required process:
1. Inspect old commits and legacy modules to identify original behavior and contracts.
2. Record the commit/file evidence for each parity-sensitive change.
3. Prefer behavior-preserving migration over redesign when conflicts appear.
4. Capture traceability in docs and issue updates so reviewers can verify legacy parity.

This requirement is mandatory input for remaining migration tasks under issue #113.

## Architecture Fit
This migration aligns to `docs/constitution.md` by implementing MAUI Shell + MVVM with strict layering:
- View (XAML) binds to ViewModel (`ObservableObject`)
- ViewModel depends on repository interfaces
- Repository implementations consume SQLite, platform services, and NDI bridge interfaces

The plan uses the module structure in constitution section 2.2 (`src/MauiApp/Features/*`, `NdiBridge`, `Data`, `Services`) and preserves architecture boundaries:
- No direct DB access from ViewModels
- No NDI native types crossing bridge boundaries
- No business logic in Views

## .NET MAUI Implementation Approach
- MAUI Shell routing / navigation changes:
  - Use canonical shell URI routes for source/home, viewer, output, and settings flows.
  - Preserve deep-link semantics through Shell route mapping.
- New pages, view models, services:
  - Ensure feature pages exist for Sources, Viewer, Output, and Settings.
  - Keep feature ViewModels in Core feature namespaces and bind via DI.
  - Use dedicated services for telemetry, navigation, and Android-specific behaviors.
- DI registration changes in `MauiProgram.cs`:
  - Register repositories, bridge implementations, and feature ViewModels via `Microsoft.Extensions.DependencyInjection`.
  - Keep singleton/transient lifetimes explicit for deterministic behavior.
- Platform-specific code:
  - Keep Android lifecycle and configuration handlers under `Platforms/Android/`.
  - Implement MediaProjection and foreground constraints in Android service implementations.

## NDI Integration
- NDI SDK capabilities used:
  - Source discovery
  - Stream receiver/viewer
  - Output/screen-share sender
- Bridge layer changes:
  - Maintain P/Invoke interop against `libndi.so` through interfaces such as discovery/viewer/output bridges.
  - Keep marshaling logic in bridge classes only.
- Threading and lifecycle constraints:
  - Marshal native-callback updates to UI thread (`MainThread.BeginInvokeOnMainThread`).
  - Stop or transition active bridge sessions safely during primary navigation changes.

## Data Layer
- SQLite schema migration approach:
  - Persist discovery server and settings state through SQLite repositories.
  - Keep async access patterns and repository abstractions consistent with constitution.
- Repository composition:
  - Maintain interface-first contracts in Core feature domain.
  - Implement persistence and runtime merge logic in repository implementations.

## Testing Strategy
- Unit test scope:
  - Repository behavior, ViewModel commands/state transitions, navigation policy and mapping.
  - Bridge interfaces mocked in unit tests (no native dependency in unit test execution).
- MAUI UI test scope:
  - App launch smoke checks, navigation destination checks, orientation-specific nav behavior.
  - Settings flow checks for save and visible state changes.
- NDI e2e validation requirements:
  - Emulator/device install validation for APK startup.
  - Runtime checks for discovery, viewer, output, and reconnect behavior.

## Risks
- Native interop mismatch risk: incorrect P/Invoke signatures or marshaling can break runtime discovery/viewer/output.
  - Mitigation: keep interop isolated and validate bridge behavior with targeted tests.
- Android lifecycle risk: foreground/background transitions can leave stale sessions.
  - Mitigation: central handoff service and lifecycle hooks in Android platform layer.
- UI automation fragility risk: locator drift after navigation changes can break Appium workflows.
  - Mitigation: maintain selectors aligned with canonical route labels and validate in PR workflows.
- CI environment drift risk: emulator/toolchain changes can cause flaky install/start behavior.
  - Mitigation: enforce uninstall-before-install and release APK install pattern in workflow scripts.

## Constitution Compliance
- Principle: Layered architecture (View -> ViewModel -> Repository -> Bridge/Data)
  - Compliance: feature code organized by constitution module boundaries and interface-driven access.
- Principle: No direct database access from ViewModels
  - Compliance: persistence only via repository implementations.
- Principle: No NDI types crossing bridge boundary
  - Compliance: bridge returns plain C# models/records; no native types in ViewModels/Views.
- Principle: MAUI Shell URI navigation
  - Compliance: route-based navigation kept in `AppShell` contracts.
- Principle: DI via `MauiProgram.cs`
  - Compliance: all cross-layer dependencies registered through MAUI DI root.
- Principle: Testing standards (happy path + error path, dotnet test gate)
  - Compliance: tests mapped to repository/viewmodel methods and CI dotnet test enforcement.
- Principle: Android-specific manifest and SDK constraints
  - Compliance: required permissions and API targets preserved per constitution section 5.
