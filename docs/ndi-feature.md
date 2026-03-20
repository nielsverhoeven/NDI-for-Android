<!-- Last updated: 2026-03-20 -->

# NDI Feature Implementation

This document describes the implemented NDI feature set in this repository, including the completed Settings Menu work from spec 006.

## Table of Contents

1. [Feature Areas](#1-feature-areas)
2. [Validation Baseline](#2-validation-baseline)
3. [Settings Menu, Discovery Configuration, and Developer Mode Overlay](#3-settings-menu-discovery-configuration-and-developer-mode-overlay)
4. [NDI SDK Integration Notes for Discovery Configuration](#4-ndi-sdk-integration-notes-for-discovery-configuration)
5. [Copy-Paste Integration Patterns](#5-copy-paste-integration-patterns)
6. [Related Documents](#6-related-documents)

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

4. `npm --prefix testing/e2e run test:pr:primary`
5. `npm --prefix testing/e2e run test:matrix`

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

## 5. Copy-Paste Integration Patterns

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

## 6. Related Documents

- Developer setup and command index: `docs/README.md`
- Architecture and dependency/data-flow diagrams: `docs/architecture.md`
- Testing strategy and commands: `docs/testing.md`
- 006 release/operator notes: `docs/006-settings-menu-release-notes.md`
- Feature spec: `specs/006-settings-menu/spec.md`
- Manual test quickstart: `specs/006-settings-menu/quickstart.md`
