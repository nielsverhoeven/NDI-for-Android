<!-- Last updated: 2026-04-07 -->

# NDI Feature Implementation

This document describes the implemented NDI feature set in this repository, including the completed Settings Menu work from spec 006.

## Table of Contents

1. [Feature Areas](#1-feature-areas)
2. [Validation Baseline](#2-validation-baseline)
3. [Settings Menu, Discovery Configuration, and Developer Mode Overlay](#3-settings-menu-discovery-configuration-and-developer-mode-overlay)
4. [NDI SDK Integration Notes for Discovery Configuration](#4-ndi-sdk-integration-notes-for-discovery-configuration)
5. [Discovery Server Submenu Pattern](#5-discovery-server-submenu-pattern)
6. [Copy-Paste Integration Patterns](#6-copy-paste-integration-patterns)
7. [Related Documents](#7-related-documents)
8. [Three-Pane Settings Workspace](#8-three-pane-settings-workspace-spec-019)
9. [Per-Source Last Frame Retention](#9-per-source-last-frame-retention-spec-023)
10. [Discovery Server Compatibility Matrix](#10-discovery-server-compatibility-matrix-spec-029)

## 1. Feature Areas

Implemented areas:

- NDI source discovery with loading, empty, success, and failure UI states.
- Foreground-only 5 second refresh scheduling plus manual refresh.
- Source selection persistence with highlight-on-relaunch and no autoplay.
- Viewer playback flow with phone/tablet adaptive layout behavior.
- Interruption handling with bounded reconnect attempts, retry, and return-to-list actions.
- Settings destination reachable from Source List, Viewer, and Output screens.
- Discovery endpoint persistence and validation (hostname, IPv4, or bracketed IPv6 with optional port).
- Developer mode toggle plus shared diagnostics overlay rendering on Source, Viewer, and Output screens.
- Discovery server management: add, edit, remove, enable/disable, and drag-to-reorder multiple discovery servers from a dedicated Settings submenu.
- Discovery servers persist in Room `discovery_servers` (DB version 6) with deterministic list order for runtime failover.
- Discovery server add/edit forms default blank port input to 5959 and prevent duplicate host+port entries.

## 2. Validation Baseline

- Android/JDK/SDK prerequisite checks: `scripts/verify-android-prereqs.ps1`
- Toolchain validation: `gradlew(.bat) --version`
- Release hardening guard: `verifyReleaseHardening` task in `app/build.gradle.kts`
- JDK toolchain: 21; Java/Kotlin bytecode target: 17

Recommended validation commands:

1. `./gradlew --version`
2. `./gradlew verifyReleaseHardening`
3. `./gradlew test connectedAndroidTest :app:assembleRelease`

Settings e2e gate references:

1. `npm --prefix testing/e2e run test:pr:primary`
2. `npm --prefix testing/e2e run test:matrix`

Release hardening policy remains mandatory:

- `:app:verifyReleaseHardening` must pass for release sign-off.
- E2E PR primary profile must complete with both new-settings and existing-regression suites passing.
- Scheduled matrix profiles must report complete results across configured profile set.

## 3. Settings Menu, Discovery Configuration, and Developer Mode Overlay

### 3.1 UI and Navigation Integration

Settings is exposed from all main screens through the `ndi://settings` deep link.

- Source List menu wiring is in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt`.
- Viewer menu wiring is in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerScreen.kt`.
- Output menu wiring is in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputControlFragment.kt`.
- Destination and deep link are defined in `app/src/main/res/navigation/main_nav_graph.xml`.

### 3.2 Settings Fragment and ViewModel Pattern

`SettingsFragment` is responsible for rendering and intent dispatch only; `SettingsViewModel` handles validation and persistence.

Source snippet: `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsFragment.kt`

```kotlin
class SettingsScreen(
	private val binding: FragmentSettingsBinding,
	onSave: () -> Unit,
	onDiscoveryChanged: (String) -> Unit,
	onDeveloperModeToggled: (Boolean) -> Unit,
) {
	init {
		binding.saveSettingsButton.setOnClickListener { onSave() }
		binding.developerModeSwitch.setOnCheckedChangeListener { _, isChecked ->
			onDeveloperModeToggled(isChecked)
		}
		binding.discoveryServerEditText.addTextChangedListener {
			onDiscoveryChanged(it?.toString().orEmpty())
		}
	}
}
```

Source snippet: `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsViewModel.kt`

```kotlin
fun onSaveSettings() {
	val state = _uiState.value
	val validationError = validateDiscoveryInput(state.discoveryServerInput)
	if (validationError != null) {
		_uiState.value = _uiState.value.copy(validationError = validationError)
		return
	}

	viewModelScope.launch {
		settingsRepository.saveSettings(
			NdiSettingsSnapshot(
				discoveryServerInput = state.discoveryServerInput.takeIf { it.isNotBlank() },
				developerModeEnabled = state.developerModeEnabled,
				updatedAtEpochMillis = System.currentTimeMillis(),
			),
		)
		SettingsDependencies.telemetryEmitter.emit(
			SettingsTelemetry.discoveryServerSaved(
				hasEndpoint = state.discoveryServerInput.isNotBlank(),
			),
		)
	}
}
```

### 3.3 Telemetry Integration Pattern

Settings-specific telemetry event factories are centralized in `SettingsTelemetry.kt` and emitted through `SettingsDependencies.telemetryEmitter`.

Implemented event names:

- `settings_opened`
- `settings_closed`
- `discovery_server_saved`
- `discovery_server_apply_immediate`
- `discovery_server_fallback_to_default`
- `active_stream_interrupted_for_discovery_apply`
- `developer_mode_toggled`
- `developer_overlay_state_changed`
- `overlay_log_redaction_applied`

## 4. NDI SDK Integration Notes for Discovery Configuration

### 4.1 Endpoint Parsing and Validation Rules

Validation comes from `NdiDiscoveryEndpoint.parse` in `core/model/src/main/java/com/ndi/core/model/NdiSettingsModels.kt`:

- Accepts `hostname`, `hostname:port`, `IPv4`, `IPv4:port`, `[IPv6]`, `[IPv6]:port`
- Rejects unbracketed IPv6 values with `:port`
- Port must be in `1..65535`
- Default port is `5960` when omitted
- Input is trimmed before validation

Discovery server submenu behavior (`DiscoveryServerRepository`) uses a separate default for the managed multi-server list:

- Blank discovery server port input defaults to `5959`
- Duplicate normalized `hostOrIp + port` entries are rejected
- Enabled servers are selected in persisted order with sequential failover

### 4.2 Persistence and Apply Path

Implemented:

- `NdiSettingsRepositoryImpl` persists `discoveryServerInput` and `developerModeEnabled` to Room (`settings_preference`).
- `NdiDiscoveryConfigRepositoryImpl` observes persisted settings and exposes parsed endpoint flow.

Current implementation note:

- `NdiDiscoveryRepositoryImpl` still uses `NdiDiscoveryBridge.discoverSources()` directly and does not currently consume `NdiDiscoveryConfigRepository` endpoint values to reconfigure native discovery behavior.
- Runtime fallback warning behavior is exposed through `SourceListDependencies.fallbackWarningProvider` and validated at integration-test seam level, but there is no production wiring in `AppGraph.kt` that emits real unreachable-endpoint fallback warnings today.

### 4.3 Stream Discovery and Interruption on Endpoint Change

Contract and telemetry support for immediate apply/interruption exist in domain and telemetry types.

Current implementation note:

- Active stream interruption on endpoint change is not currently executed by `SettingsViewModel` or discovery repository wiring in this codebase snapshot.

## 5. Discovery Server Submenu Pattern

Primary files:

- `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsFragment.kt`
- `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/DiscoveryServerSettingsViewModel.kt`
- `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/DiscoveryServerRepositoryImpl.kt`
- `core/database/src/main/java/com/ndi/core/database/NdiDatabase.kt`

Operator behavior summary:

- Discovery Servers submenu is reachable from Settings via deep link `ndi://settings/discovery-servers`.
- Users can add, edit, delete, reorder, and toggle individual entries.
- Runtime target resolution iterates enabled entries in persisted order.
- If all enabled entries are unreachable, the runtime returns an explicit failure result.
- After each add, the app performs a discovery check and stores per-server check status (`Connected` or `Check failed`) with timestamp and failure reason.
- Each discovery server row exposes a recheck action that updates only that row's check status.
- Developer diagnostics include discovery check rollup and latest discovery refresh status, and are hidden when developer mode is disabled.


## 6. Copy-Paste Integration Patterns

### 5.1 Add a New Settings Screen Using Existing Pattern

To add a new settings screen, follow the same dependency-locator + ViewModel factory pattern used by `SettingsFragment.kt` and `SettingsDependencies`.

```kotlin
class MySettingsFragment : Fragment() {
	private val viewModel: MySettingsViewModel by viewModels {
		MySettingsViewModel.Factory(SettingsDependencies.requireSettingsRepository())
	}

	override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
		viewLifecycleOwner.lifecycleScope.launch {
			viewLifecycleOwner.repeatOnLifecycle(Lifecycle.State.STARTED) {
				viewModel.uiState.collect { state ->
					// render(state)
				}
			}
		}
	}
}
```

### 5.2 Register Dependencies in AppGraph

Source pattern: `app/src/main/java/com/ndi/app/di/AppGraph.kt`

```kotlin
SettingsDependencies.settingsRepositoryProvider = { settingsRepository }
SettingsDependencies.developerDiagnosticsRepositoryProvider = { developerDiagnosticsRepository }
SettingsDependencies.overlayStateProvider = { overlayDisplayStateFlow }
```

### 5.3 Reuse Overlay Rendering on Any Screen

```kotlin
DeveloperOverlayRenderer.render(
	container = binding.developerOverlay.developerOverlayContainer,
	streamStatusView = binding.developerOverlay.overlayStreamStatus,
	sessionIdView = binding.developerOverlay.overlaySessionId,
	recentLogsView = binding.developerOverlay.overlayRecentLogs,
	overlayDisplayState = state.overlayDisplayState,
)
```

## 7. Related Documents

- Developer setup and command index: `docs/README.md`
- Architecture and dependency/data-flow diagrams: `docs/architecture.md`
- Testing strategy and commands: `docs/testing.md`
- 006 release/operator notes: `docs/006-settings-menu-release-notes.md`
- Feature spec: `specs/006-settings-menu/spec.md`
- Discovery server management spec: `specs/018-manage-discovery-servers/spec.md`
- Manual test quickstart: `specs/006-settings-menu/quickstart.md`

## 8. Three-Pane Settings Workspace (Spec 019)

Implemented behavior summary:

- Wide-layout settings now support a three-pane workspace:
	- column 1: main navigation actions (Home, Stream, View, Settings)
	- column 2: settings category list with selected-state highlighting
	- column 3: in-place detail panel that updates when category changes
- Compact settings fallback remains active when wide-layout criteria are not met.
- Selected category context is preserved across compact/wide transitions.
- Empty categories render explicit empty-state feedback in the detail panel.

Primary implementation files:

- `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsFragment.kt`
- `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsViewModel.kt`
- `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsLayoutResolver.kt`
- `feature/ndi-browser/presentation/src/main/res/layout/fragment_settings_three_pane.xml`
- `feature/ndi-browser/presentation/src/main/res/layout/view_settings_main_navigation_panel.xml`

Validation snapshot for spec 019:

- Unit test and release-hardening gates passed.
- Playwright validation commands executed but classified as `BLOCKED-ENV` due emulator UIAutomator dump instability.
- See `test-results/019-settings-three-pane-validation.md` for command logs and unblock steps.

## 9. Per-Source Last Frame Retention (Spec 023)

Implemented behavior summary:

- Source-list previews now retain the last captured frame independently per viewed source rather than sharing a single global preview slot.
- Retention is session-scoped only: thumbnails are written under `cacheDir/ndi-session-previews`, mirrored in-memory, and discarded when the app process ends.
- The retention store is capped at 10 sources and evicts the least-recently-viewed thumbnail when a new source exceeds that cap.
- Thumbnail capture happens when the viewer stops for a source, after the existing single-source continuity preview has been persisted.
- The existing relaunch continuity path remains unchanged and is still owned by `ViewerContinuityRepository`.

Primary implementation files:

- `feature/ndi-browser/domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt`
- `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/PerSourceFrameRepositoryImpl.kt`
- `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiViewerRepositoryImpl.kt`
- `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModel.kt`
- `app/src/main/java/com/ndi/app/di/AppGraph.kt`

Validation snapshot for spec 023:

- `:app:assembleDebug` passed and exported a debug APK.
- `:feature:ndi-browser:data:testDebugUnitTest`, `:feature:ndi-browser:presentation:testDebugUnitTest`, and the continuity regression tests passed.
- `:app:verifyReleaseHardening` passed.
- Release bundle compilation remains blocked by a pre-existing DataBinding release-resource issue unrelated to spec 023.
- Playwright coverage for spec 023 was intentionally deferred to the next feature specification cycle.

## 10. Discovery Server Compatibility Matrix (Spec 029)

Implemented behavior summary:

- Discovery compatibility outcomes are classified using shared status taxonomy: `compatible`, `limited`, `incompatible`, `blocked`.
- Discovery now persists per-target compatibility outcomes into an in-memory matrix repository keyed by target id.
- Mixed-server outcomes preserve usable sources while recording non-compatible endpoints and aggregated partial outcomes.
- Developer diagnostics now include actionable compatibility guidance lines (target, status, next step) through existing diagnostics surfaces.
- Overlay mapping and rendering preserve compatibility diagnostics without introducing a dedicated UI surface.

Primary implementation files:

- `core/model/src/main/java/com/ndi/core/model/DeveloperDiscoveryDiagnostics.kt`
- `feature/ndi-browser/domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt`
- `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/DiscoveryCompatibilityMatrixRepository.kt`
- `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/NdiDiscoveryRepositoryImpl.kt`
- `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/repository/DeveloperDiagnosticsRepositoryImpl.kt`
- `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/DeveloperOverlayState.kt`
- `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/DeveloperOverlayRenderer.kt`
- `app/src/main/java/com/ndi/app/di/AppGraph.kt`

Operational validation commands:

1. `./gradlew.bat :feature:ndi-browser:data:testDebugUnitTest --tests "com.ndi.feature.ndibrowser.data.DiscoveryCompatibilityMatrixRepositoryTest" --tests "com.ndi.feature.ndibrowser.data.DeveloperDiagnosticsRepositoryImplTest"`
2. `./gradlew.bat :feature:ndi-browser:presentation:testDebugUnitTest --tests "com.ndi.feature.ndibrowser.settings.DeveloperOverlayStateMapperTest" --tests "com.ndi.feature.ndibrowser.settings.DiscoveryServerSettingsViewModelTest"`
3. `pwsh -NoProfile -ExecutionPolicy Bypass -File .\testing\e2e\scripts\run-discovery-compatibility-matrix.ps1 -Profiles pr-primary`

Known runtime limitation:

- Full per-version matrix execution for baseline-latest and venue-failing remains blocked until concrete endpoint host/version values are available in the feature validation target list.

Validation status:

- Code-level compatibility behavior: pass.
- Diagnostics mapping and Playwright contract/profile checks: pass.
- Runtime per-target baseline/venue matrix: blocked pending endpoint and version capture.
- Evidence references:
	- `test-results/029-compatibility-matrix.md`
	- `test-results/029-final-validation-summary.md`
