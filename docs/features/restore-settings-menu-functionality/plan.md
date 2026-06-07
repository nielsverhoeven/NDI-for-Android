<!-- Last updated: 2026-06-07 -->

# Technical Plan: Restore Missing and Broken Settings Menu Functionality

## Architecture Fit
This plan aligns with the constitution's layered architecture and MAUI module boundaries:
- Views remain presentation-only in MAUI XAML pages under Settings.
- ViewModels own settings state transitions, validation triggers, and command orchestration.
- Repository interfaces remain the only path from ViewModel to persistence.
- Persistence and platform-specific behavior stay outside the ViewModel layer.

Constitution alignment points:
- Layering and no business logic in views: enforce state and validation logic in ViewModels.
- No direct database access from ViewModels: continue using settings repository abstraction.
- Testing standards: provide happy-path and error-path tests for modified public behavior.
- Navigation consistency: retain Settings route via Shell navigation contract.

## .NET MAUI Implementation Approach
- MAUI Shell routing and navigation changes:
  - Validate and preserve navigation contract for the Settings destination and existing entry points from Source, Viewer, and Output flows.
  - Ensure route registration remains stable and discoverable through AppShell.
- New pages, view models, services:
  - Implement a two-pane settings workspace matching issue screenshots:
    - left pane: category cards (`General`, `Appearance`, `Discovery`, `Developer tools`, `About`)
    - right pane: selected section detail panel
    - global bottom `Apply` action affecting current staged changes
  - Extend the Settings page composition to represent required sections from baseline.
  - Expand Settings view model state and commands to support restored controls and section-level validation.
  - Add or extend supporting services only where UI state composition requires non-repository dependencies.
- DI registration changes in MauiProgram.cs:
  - Register any new settings-focused abstractions using interface-first patterns.
  - Keep repository and service lifetimes consistent with existing settings behavior.
- Platform-specific code:
  - If a setting depends on Android-specific capability, isolate behavior behind interface abstractions and implement under Platforms/Android.
  - Avoid embedding Android API calls directly in ViewModels or XAML code-behind.

## NDI Integration (if applicable)
- Applicable scope:
  - Discovery-related settings can affect NDI discovery behavior and must integrate safely with discovery bridge consumers.
- Bridge layer changes:
  - Preserve existing bridge boundary contract where only plain C# models flow into application layers.
  - If discovery configuration application changes are required, route them through repository/service orchestration rather than direct view coupling.
- Threading and lifecycle constraints:
  - Apply configuration updates without violating active session lifecycle constraints.
  - Ensure UI-bound state updates remain on the UI thread where required.

## Data Layer
- Persistence approach:
  - Continue using repository-mediated persistence over existing SQLite-backed data access.
- Schema and model changes:
  - Additive persistence model updates only if required to store missing settings state.
  - Maintain backward compatibility for existing stored preferences and safe defaulting for new nullable fields.
- Repository behavior:
  - Guarantee deterministic load/save mapping between persistence model and ViewModel state.
  - Preserve validation-before-save rules to prevent corrupt or invalid persisted data.

## Required Settings Baseline Matrix
The implementation and validation baseline is fixed to issue #142 artifacts and current settings documentation. Any deviation must be explicitly documented as an intentional exclusion.

| Section | Control | Required or Optional | Validation Rule | Persistence Key | Default Value | Intentional Exclusion Note |
|---|---|---|---|---|---|---|
| General | Discovery Host | Required | Host may be empty (use runtime default) or a valid hostname or IP | `DiscoveryHost` | `null` | None |
| General | Discovery Port | Required | Empty allowed, otherwise integer in range 1..65535 | `DiscoveryPort` | `null` | None |
| General | Save action | Required | Must reject invalid input and surface validation message | N/A (command) | N/A | None |
| Appearance | Theme mode selector | Required | Value must be in supported enum set | `ThemeMode` | `System` | None |
| Discovery | Discovery servers list | Required | Row host and port must pass host and port validation | `DiscoveryServers[]` | Empty list | None |
| Discovery | Add or edit server | Required | Reject duplicate normalized host plus port entries | `DiscoveryServers[]` | N/A | None |
| Discovery | Enable or disable server | Required | Toggle state persists and reloads deterministically | `DiscoveryServers[].Enabled` | `true` | None |
| Discovery | Reorder server priority | Required | Persisted order matches UI order after restart | `DiscoveryServers[].Order` | Insert order | None |
| Developer Tools | Developer mode toggle | Required | Boolean state saves and reloads without crash | `DeveloperModeEnabled` | `false` | None |
| About | App info block | Required | Must render app name and version metadata without runtime errors | N/A | N/A | None |

## Screenshot Conformance Requirements
The issue screenshots are now treated as the primary UX parity target for issue #142.

1. Global layout and interaction model:
  - Left-side category card list includes exactly five visible categories:
    - `General`
    - `Appearance`
    - `Discovery`
    - `Developer tools`
    - `About`
  - Right pane shows section-specific content for the selected category.
  - `Apply` button is centered near bottom of detail area and is disabled until a mutable setting changes.
  - Selecting categories does not immediately persist; persistence occurs through explicit `Apply`.

2. General section parity:
  - Right pane title: `General Settings`.
  - Guidance text communicates adjusting settings by category and applying to save.

3. Appearance section parity:
  - Right pane title: `Appearance Settings`.
  - Theme mode radio group includes `Light`, `Dark`, `System default`.
  - Accent color radio group includes `Blue`, `Teal`, `Green`, `Orange`, `Red`, `Pink`.

4. Discovery section parity:
  - Host or IP input and separate port input with hint `Port (default 5959)`.
  - `Add Server` action available.
  - Server list rows include reorder affordance, endpoint text, enable toggle, edit action, and delete action.

5. Developer tools section parity:
  - Right pane title: `Developer Settings`.
  - `Developer Mode` toggle appears in detail pane.
  - `Cached Source Registry` list renders entries with fields: source name, endpoint, state, key, and last-seen timestamp.

6. About section parity:
  - Right pane title: `About`.
  - App version is displayed in `name` plus `version (build)` style.

## Navigation Contract Assertions
The route contract must remain stable and explicitly verifiable.

- Settings route identity:
  - Route stays `//settings` for primary navigation destination.
  - Deep-link or route aliases can be added, but must not replace `//settings`.
- Entry-path reachability assertions:
  - From Sources flow, invoking settings navigation lands on Settings page with no exception.
  - From Viewer flow, invoking settings navigation lands on Settings page with no exception.
  - From Output flow, invoking settings navigation lands on Settings page with no exception.
- Route regression checks:
  - Add unit or UI checks that assert route registration and resolution for `//settings`.
  - Add smoke checks that execute navigation from all three entry points.

## Persistence Compatibility and Migration
- Additive-field strategy:
  - New settings fields must be additive and nullable-safe where possible.
  - Existing snapshots must deserialize or map without requiring destructive migration.
- Legacy defaulting rules:
  - Missing boolean fields default to safe disabled state (`false`) unless explicitly required enabled.
  - Missing enum fields default to stable baseline (`System` for theme mode).
  - Missing collection fields default to empty collection.
- Malformed data handling:
  - Repository load must fail soft by returning sanitized defaults and logging telemetry context.
  - Repository save must reject invalid values before persistence.
- Test requirements:
  - Add explicit error-path tests for malformed persisted records.
  - Add round-trip tests for legacy snapshot -> upgraded model -> persisted snapshot.
  - Keep all persistence paths async-only per constitution.

## DI Registration Decisions
All DI changes remain centralized in `MauiProgram.cs` with explicit lifetimes.

| Abstraction | Implementation | Lifetime | Decision |
|---|---|---|---|
| `ISettingsRepository` | `SettingsRepository` | Singleton | Keep existing lifetime |
| `SettingsViewModel` | `SettingsViewModel` | Transient | Keep existing lifetime |
| `IThemeSettingsService` (if introduced) | `ThemeSettingsService` | Singleton | New registration if appearance settings restoration requires shared state |
| `IDiscoveryServersRepository` (if split from settings repository) | `DiscoveryServersRepository` | Singleton | New registration only if list management is extracted |
| `ISettingsValidationService` (if introduced) | `SettingsValidationService` | Singleton | New registration if validation logic is centralized across sections |

## Testing Strategy
- Unit test scope:
  - Settings ViewModel category selection and staged-change behavior for each restored section.
  - Validation logic for invalid inputs and error messaging.
  - Repository mapping tests for persisted snapshot round-trip behavior.
- MAUI UI test scope:
  - Settings page renders required categories and detail-pane controls matching screenshot parity checklist.
  - Settings controls are interactive and save feedback is visible.
  - `Apply` enabled/disabled state transitions are validated.
  - Restart-oriented smoke check where automation environment allows persisted-value verification.
- NDI e2e validation requirements:
  - For discovery-impacting settings, validate runtime discovery behavior on device/emulator with settings reapplied.
  - Capture install/launch/runtime evidence when acceptance depends on device-visible behavior.

## Risks
- Baseline mismatch risk:
  - Risk: ambiguity about exact expected section/control set.
  - Mitigation: anchor implementation and validation to issue artifacts and documented release baselines.
- Persistence regression risk:
  - Risk: restoring missing controls without durable persistence mapping.
  - Mitigation: add round-trip repository tests and restart validation checks.
- Navigation regression risk:
  - Risk: settings route or entry points break while restoring sections.
  - Mitigation: include route reachability checks from all main feature entry points.
- Discovery side-effect risk:
  - Risk: applying discovery-related settings interrupts or desynchronizes discovery state.
  - Mitigation: stage changes through orchestrated service/repository updates and verify with targeted NDI behavior checks.

## Constitution Compliance
- Principle: No business logic in Views.
  - Compliance: keep section rendering in XAML and move all behavior to ViewModel commands/state.
- Principle: No direct database access from ViewModels.
  - Compliance: all persistence remains through settings repository interfaces.
- Principle: Bridge boundary returns plain C# models only.
  - Compliance: NDI-related settings integration does not leak native types into ViewModel/UI layers.
- Principle: DI through MauiProgram.cs without service locator.
  - Compliance: register or update dependencies centrally in DI root.
- Principle: Test standards and build hygiene.
  - Compliance: add or update unit/UI coverage for restored behavior and require passing build/test gates before completion.

---

## Completion Summary

**Status: Complete** — branch `feature/142-recreate-settings-menu`, issue #142

### Task Outcomes (T001–T009)

All implementation tasks are complete and verified by unit tests. See `tasks.md` for the full task-by-task status.

| Task | Status | Evidence |
|---|---|---|
| T001 — Data contracts and defaults | Done | `SettingsModels.cs`: `ThemeMode`, `AccentColorOption`, `DiscoveryServerPreference`, `CachedSourceRegistryEntry`, `SettingsAppInfo`, `NdiSettingsSnapshot` with `CreateDefault()` |
| T002 — Repository compatibility and persistence hardening | Done | `SettingsRepository.cs`: sanitize-before-save, exception fallback to defaults, atomic Apply |
| T003 — General section and global Apply workflow | Done | `SettingsViewModel`: `ApplyCommand`, `CanApply`, `IsDirty`, `IsApplied`, `ValidationError`, `GeneralGuidanceText` |
| T004 — Appearance section | Done | Theme radio group (Light/Dark/System default); accent color radio group (Blue/Teal/Green/Orange/Red/Pink); staged selection |
| T005 — Discovery section | Done | Host/port inputs, Add/Update/Delete, Up/Down reorder, per-server enable toggle, duplicate host+port detection |
| T006 — Developer Tools section | Done | `DeveloperModeEnabled` toggle; Cached Source Registry list via `ISourceRepository.GetCachedSourcesAsync` |
| T007 — About section | Done | `SettingsAppInfo` via `ISettingsPlatformService.GetAppInfo()`; displayed in `name version (build)` format |
| T008 — Two-pane shell composition | Done | `SettingsPage.xaml`: 220-pt left column of category buttons; right pane with `IsVisible` section switching |
| T009 — Screenshot-parity tests | Done | 109 unit tests passing across five test classes |

### Pending (Outside Implementation Scope)

- **T010 — GitHub status sync**: Manual comment and close action on parent issue #142 and sub-issues #201–#210. Not automated.

### Key Architectural Decisions Made

1. **`ISettingsValidationService`** introduced as a singleton to centralize host/port/duplicate validation away from the ViewModel.
2. **`IDiscoverySettingsOrchestrator`** introduced as a singleton; resolves primary host or first-enabled server and calls `INdiDiscoveryBridge.SetDiscoveryEndpoint` on every load and Apply.
3. **`ISettingsPlatformService`** introduced to isolate `AppInfo.Current` calls behind a testable boundary; Android and non-Android implementations registered conditionally in `MauiProgram.cs`.
4. **`SettingsViewModel`** remains Transient; all stateful singletons are repository/service layer only.
5. **Settings route**: `//settings` (portrait), `//settings-rail` (landscape) — both resolved by `AppShell.xaml.cs` adaptive routing logic.

### Environment Notes

Device/emulator runtime validation (Appium) was not executable in the CI environment used for this cycle. Compile-target build and 109-passing unit tests were used as the acceptance gate. See `docs/006-settings-menu-release-notes.md` for the full release record.
