<!-- Last updated: 2026-06-07 -->

# Release Notes: 006 Settings Menu

## Scope

This release adds the Settings UI surface, discovery configuration persistence/validation, and a developer diagnostics overlay path across Source, Viewer, and Output screens.

Spec 018 extends settings with a dedicated Discovery Servers submenu for multi-server management.

## User-Facing Features

- Open Settings from main screens (Source List, Viewer, Output) via settings menu action.
- Configure custom discovery endpoint input (hostname, IPv4, bracketed IPv6, optional port).
- Toggle Developer Mode on/off from Settings.
- Show diagnostics overlay on main screens when Developer Mode is enabled.
- Overlay content includes stream status, redacted session ID, and recent redacted logs.
- Open Discovery Servers submenu from Settings.
- Add/edit/remove multiple discovery servers with separate host and port fields.
- Leave port blank to default to 5959.
- Toggle each server enabled/disabled independently.
- Drag and reorder saved servers to control runtime failover priority.

## Operator and Admin Notes

- Settings are persisted locally in Room (`settings_preference` table).
- No server-side sync exists; settings are device-local only.
- Sensitive diagnostics data is redacted before display:
  - IPv4 and IPv6 values are replaced with `[redacted-ip]`.
  - Session IDs are masked to `****` + last 4 characters.
- Telemetry events are emitted for settings open/close, save, toggles, and overlay transitions.

Discovery server operations:

- Discovery server rows are persisted in Room `discovery_servers` and survive app restart.
- Duplicate host+port entries are rejected with inline validation feedback.
- Runtime target selection iterates enabled servers in persisted order.
- If all enabled servers are unreachable, runtime returns explicit failure guidance.

Current implementation note:

- Fallback warning and immediate apply/interruption semantics are represented in spec/contracts/telemetry and test seams, but production runtime wiring for endpoint reachability fallback and bridge reconfiguration is still incomplete in current code.

## Deprecations

- None.

## Known Limitations

- Dual-emulator e2e validation remains environment-dependent and may be pending where emulator/device setup is unavailable.
- Instrumentation timing tests for overlay visibility/status are currently scaffold placeholders.
- Playwright pre-install may fail if auto-incremented app version and pre-install expected version drift; rerun with aligned version metadata.

## Verification Snapshot

- Unit tests: green for settings models/viewmodels and redaction/state mapper.
- Compile and release hardening gates: configured and passing in recorded test results.
- E2E: harness and scripts are available through `testing/e2e/README.md`.

## Spec 019 Addendum: Three-Pane Settings Workspace

New behavior:

- Adds wide-layout three-pane settings workspace (navigation, categories, details).
- Maintains compact fallback for unsupported layouts.
- Preserves selected category context across layout transitions.

Validation status:

- Android unit tests: PASS
- Release hardening (`:app:verifyReleaseHardening :app:assembleRelease`): PASS
- Playwright regression and feature spec suite: BLOCKED-ENV in current run due emulator UI hierarchy dump instability.

## SC-004 Feedback Query and One-Cycle Comparison Method

Baseline query (pre-release cycle):

```text
source = support_feedback
AND app = "NDI-for-Android"
AND release_cycle = "pre-019"
AND tags CONTAINS ANY ("settings", "navigation", "large-screen", "tablet")
AND sentiment = "negative"
```

Post-release query (first cycle after release):

```text
source = support_feedback
AND app = "NDI-for-Android"
AND release_cycle = "post-019"
AND tags CONTAINS ANY ("settings", "navigation", "large-screen", "tablet")
AND sentiment = "negative"
```

Comparison method:

1. Count matching records for pre-019 and post-019 cycles.
2. Compute relative delta: `(post - pre) / pre * 100`.
3. SC-004 passes when delta is `<= -30%`.

## Issue #142 Addendum: Restore Settings Menu Functionality

**Status: Released** — branch `feature/142-recreate-settings-menu`

### Delivered Sections

The MAUI Settings page is implemented as a two-pane layout: a fixed 220-pt left column of category buttons and a right detail pane that swaps content on selection.

| Section | Detail-pane title | Key controls |
|---|---|---|
| General | General Settings | Guidance text; global staged Apply workflow (dirty tracking, validation-gated CanApply, apply-completion feedback) |
| Appearance | Appearance Settings | Theme radio group (Light / Dark / System default); accent color radio group (Blue / Teal / Green / Orange / Red / Pink) |
| Discovery | Discovery | Primary host and port inputs; Add / Update / Delete server list; Up / Down reorder; per-server enable toggle; duplicate host+port detection |
| Developer Tools | Developer Settings | Developer Mode toggle; Cached Source Registry list (SourceName, Endpoint, State, RegistryKey, LastSeen) |
| About | About | App name and version displayed in `name version (build)` style |

### New Files Delivered

**`src/Core/Features/Settings/`**

| File | Purpose |
|---|---|
| `Models/SettingsModels.cs` | `ThemeMode`, `AccentColorOption`, `DiscoveryServerPreference`, `CachedSourceRegistryEntry`, `SettingsAppInfo`, `NdiSettingsSnapshot` |
| `Services/IDiscoverySettingsOrchestrator.cs` | Contract for applying discovery endpoint to the NDI bridge |
| `Services/ISettingsPlatformService.cs` | Contract for reading platform app metadata |
| `Services/SettingsValidationService.cs` | `ISettingsValidationService`: `Sanitize`, `TryValidateForSave`, `IsValidHostOrEmpty` |
| `ViewModels/DiscoveryServerItem.cs` | Observable wrapper for a single discovery server row |
| `ViewModels/SettingsViewModel.cs` | Full ViewModel: section selection, staged Apply, dirty tracking, all section state |

**`src/MauiApp/Features/Settings/`**

| File | Purpose |
|---|---|
| `Services/DiscoverySettingsOrchestrator.cs` | Resolves primary host or first-enabled server, calls `INdiDiscoveryBridge.SetDiscoveryEndpoint` |
| `Views/SettingsPage.xaml` | Two-pane XAML composition; five section buttons; detail pane switching via `IsVisible` bindings |
| `Repositories/SettingsRepository.cs` | Staged Apply with sanitize-before-save, malformed-data fallback to `NdiSettingsSnapshot.CreateDefault()` |

**`src/MauiApp/`**

| File | Purpose |
|---|---|
| `Platforms/Android/Services/AndroidSettingsPlatformService.cs` | Android implementation of `ISettingsPlatformService` using `AppInfo.Current` |
| `Services/DefaultSettingsPlatformService.cs` | Non-Android fallback implementation of `ISettingsPlatformService` |

### Modified Files

- `Data/NdiDatabase.cs` — `GetSettingsAsync` / `SaveSettingsAsync`
- `AppShell.xaml` + `AppShell.xaml.cs` — settings shell routes (`//settings` portrait, `//settings-rail` landscape)
- `MauiProgram.cs` — DI registrations (see DI Registration Decisions in the feature plan)
- `Features/Sources/Repositories/SourceRepository.cs` — `GetCachedSourcesAsync`

### DI Registrations (MauiProgram.cs)

| Abstraction | Implementation | Lifetime |
|---|---|---|
| `ISettingsRepository` | `SettingsRepository` | Singleton |
| `ISettingsValidationService` | `SettingsValidationService` | Singleton |
| `IDiscoverySettingsOrchestrator` | `DiscoverySettingsOrchestrator` | Singleton |
| `ISettingsPlatformService` | `AndroidSettingsPlatformService` (Android) / `DefaultSettingsPlatformService` (other) | Singleton |
| `SettingsViewModel` | `SettingsViewModel` | Transient |

### Test Results

- **Unit tests: 109 passing** (expanded from 31 prior to this feature)
- Test classes added: `SettingsViewModelTests`, `SettingsValidationServiceTests`, `DiscoveryServerItemTests`, `SettingsValidationServiceExtendedTests`, `SettingsViewModelDirtyTrackingTests`
- Coverage includes: load from repository, staged Apply, validation error paths, duplicate-server detection, dirty-tracking state transitions
- Device/emulator runtime validation (Appium) was not executable in the CI environment; compile-target and unit-test gates were used as acceptance gates in this cycle.
