# API Contract: Quality Profile Management

**Phase**: 1 (Design - define public interfaces)  
**Date**: March 28, 2026  
**Module**: feature/ndi-browser/domain

---

## Overview

This contract defines the public interface for quality profile selection and preference management. It serves as the boundary between presentation layer (ViewerViewModel) and data layer (QualityProfileRepository), ensuring loose coupling and testability.

---

## Interface: QualityProfileRepository

**Location**: `feature/ndi-browser/domain/repository/QualityProfileRepository.kt`

**Responsibility**: Manage quality preset configuration and user preference persistence.

```kotlin
interface QualityProfileRepository {
    
    /**
     * Retrieve all available quality profiles.
     * 
     * @return List of three preset profiles (Smooth, Balanced, High Quality)
     *         ordered by priority (ascending)
     * @throws Exception if profile definitions cannot be loaded
     */
    suspend fun getAllProfiles(): List<QualityProfile>
    
    /**
     * Get the current quality preference for playback.
     * 
     * @param sourceId Optional NDI source ID. If null, returns global default preference.
     *                  If provided, returns source-specific preference if saved,
     *                  otherwise falls back to global default.
     * @return QualityPreference containing selected profile ID and timestamp
     * @throws Exception if preference cannot be loaded from storage
     */
    suspend fun getQualityPreference(sourceId: String? = null): QualityPreference
    
    /**
     * Save user's quality preference to persistent storage.
     * 
     * @param preference QualityPreference with selected profile ID
     *                   If sourceId is null, saves as global default.
     *                   If sourceId is provided, saves source-specific override.
     * @throws Exception if preference cannot be saved to storage
     */
    suspend fun setQualityPreference(preference: QualityPreference)
    
    /**
     * Clear all stored preferences (global and source-specific).
     * Returns to factory defaults on next read.
     * 
     * @throws Exception if preferences cannot be cleared
     */
    suspend fun clearPreferences()
}
```

---

## Interface: NdiViewerRepository Extensions

**Location**: `feature/ndi-browser/domain/repository/NdiRepositories.kt` (extend existing)

**New methods for quality-aware playback control**:

```kotlin
interface NdiViewerRepository {
    
    // ... existing methods ...
    
    /**
     * Apply a quality profile to the active NDI stream.
     * 
     * Coordinates with native NDI SDK to:
     * - Select preferred codec (H.264 vs H.265 based on profile)
     * - Set target resolution
     * - Set frame rate cap
     * 
     * Does NOT control bitrate (NDI SDK handles adaptive bitrate).
     * Frame drops are monitored separately by frame-monitoring loop.
     * 
     * @param profile QualityProfile containing codec/resolution/frameRate targets
     * @return Unit on success
     * @throws IllegalStateException if no stream is currently active
     * @throws UnsupportedOperationException if codec not supported on device
     */
    suspend fun applyQualityProfile(profile: QualityProfile)
    
    /**
     * Get real-time playback optimization statistics.
     * 
     * Collects metrics from NDI SDK:
     * - Actual achieved frame rate (FPS counter in last 1-sec window)
     * - Actual resolution (may differ from target if network-limited)
     * - Buffer fill percentage
     * - Frame drop count
     * 
     * @return Flow<PlaybackOptimization> emitting updates at ~1 Hz
     * @throws IllegalStateException if no stream is currently active
     */
    fun getPlaybackOptimizationFlow(): Flow<PlaybackOptimization>
    
    /**
     * Downgrade quality profile on detected frame drops.
     * 
     * Called by ViewerViewModel when frameDropPercentage exceeds threshold.
     * Automatically selects next lower-priority profile and applies it.
     * User notification handled in presentation layer.
     * 
     * @param currentProfile The profile currently in use
     * @return New profile applied, or null if already at minimum (Smooth)
     * @throws IllegalStateException if no stream is currently active
     */
    suspend fun degradeQuality(currentProfile: QualityProfile): QualityProfile?
    
    /**
     * Handle stream disconnection with recovery attempt.
     * 
     * Coordinates with NDI SDK to:
     * - Pause rendering
     * - Attempt reconnection with exponential backoff (max 5 attempts, 3-30 second intervals)
     * - Restore original quality preference after reconnection
     * 
     * @param sourceId NDI source that disconnected
     * @param maxRetries Maximum reconnection attempts (default 5)
     * @return true if reconnection successful, false if exhausted all retries
     * @throws Exception if reconnection fails permanently
     */
    suspend fun handleStreamDisconnection(sourceId: String, maxRetries: Int = 5): Boolean
}
```

---

## Interface: PlayerScalingCalculator

**Location**: `feature/ndi-browser/presentation/viewer/PlayerScalingCalculator.kt`

**Responsibility**: Calculate video scaling dimensions for aspect-ratio-preserving layout.

```kotlin
interface PlayerScalingCalculator {
    
    /**
     * Calculate scaled video dimensions that preserve aspect ratio
     * while fitting within available player bounds.
     * 
     * ALGORITHM:
     * 1. Compare stream aspect ratio to container aspect ratio
     * 2. If stream is wider: scale to container width, letterbox top/bottom
     * 3. If stream is taller: scale to container height, letterbox left/right
     * 4. Return dimensions that maximize content fill (target >= 90%)
     * 
     * @param state PlayerScalingState containing bounds and stream aspect ratio
     * @return ScaledDimensions with width, height, and letterbox side info
     */
    fun calculateScaledDimensions(state: PlayerScalingState): ScaledDimensions
    
    /**
     * Verify scaled dimensions meet minimum space utilization target.
     * 
     * @param state PlayerScalingState with current bounds
     * @return true if utilization >= 90%, false otherwise
     */
    fun meetsUtilizationTarget(state: PlayerScalingState): Boolean
}
```

---

## State Flow Contract

### Flow Emissions from Repository

**Pattern**: Cold flow (emits only when collected)

```
ViewerViewModel collects:

getPlaybackOptimizationFlow() 
  → PlaybackOptimization emitted at ~1 Hz during active playback
  → Frame rate, buffer %, frame drops captured from NDI SDK metrics
  → ViewModel uses emissions to:
     a) Drive UI updates (frame rate display, buffer progress bar)
     b) Detect frame drops and trigger auto-degradation if necessary
     c) Track quality switching decisions for telemetry

getQualityPreference(sourceId)
  → Emitted once on ViewModel init or when user selects profile
  → Cached in ViewModel until user changes or stream changes

disconnectionEvent: SharedFlow<DisconnectionEvent>
  → Emitted on stream disconnection, reconnection attempt, success/failure
  → ViewModel collects to show recovery dialog
  → Dialog provides manual "Reconnect" button that triggers handleStreamDisconnection()
```

---

## Contracts by Layer

### Data Layer → Domain Layer

**SharedPreferencesQualityStore** must implement:

```kotlin
interface IQualityStore {
    fun save(preference: QualityPreference)
    fun load(sourceId: String? = null): QualityPreference
    fun clear()
}
```

### Domain Layer → Presentation Layer

**ViewModel receives** from repository:

```
1. List<QualityProfile> for UI menu rendering (static, loaded once)
2. Flow<PlaybackOptimization> for real-time metrics display
3. Flow<DisconnectionEvent> for recovery dialog triggering
4. QualityPreference for initial UI state (profile selection indicator)
```

**ViewModel sends** to repository:

```
1. applyQualityProfile(profile) on user selection
2. setQualityPreference(preference) on user selection (persisted)
3. handleStreamDisconnection() on manual reconnect button click
```

---

## Error Handling Contract

### Success Paths

| Method | Success | Return | Side Effects |
|--------|---------|--------|--------------|
| `applyQualityProfile()` | Applied | Unit | NDI SDK receives codec/resolution/FPS commands |
| `getPlaybackOptimizationFlow()` | Subscribed | Flow<...> | Metrics collection begins at 1 Hz |
| `handleStreamDisconnection()` | Reconnected | true | NDI stream resumed, original quality restored |
| `degradeQuality()` | Downgraded | QualityProfile | Lower-priority profile applied immediately |

### Error Paths

| Method | Error | Thrown | Handling |
|--------|-------|--------|----------|
| `applyQualityProfile()` | No stream active | IllegalStateException | ViewModel catches, shows "Not connected" dialog |
| `applyQualityProfile()` | Unsupported codec | UnsupportedOperationException | ViewModel catches, falls back to H.264 |
| `getPlaybackOptimizationFlow()` | No stream active | IllegalStateException | ViewModel catches in try-catch block |
| `handleStreamDisconnection()` | Net error | Exception | Logged, counted toward retry limit, shown in dialog |
| `handleStreamDisconnection()` | Max retries exceeded | (returns false) | ViewModel shows "Reconnection failed" dialog |

### Recovery Expectations

- **Transient network error** (e.g., WiFi blip): Retry succeeds within 3 attempts
- **Codec mismatch**: ViewModel auto-falls-back to H.264 and re-triggers applyQualityProfile()
- **User network change** (WiFi → cellular): Frame monitoring auto-detects drops, triggers degradeQuality()
- **Complete disconnection**: handleStreamDisconnection() exhausts 5 retries, shows manual reconnect dialog

---

## Thread Safety Contract

All suspension functions are **safe to call from Coroutine context**:

- Repository methods execute on `Dispatchers.IO` (not blocking UI thread)
- SharedPreferences access wrapped in `withContext(Dispatchers.IO)`
- NDI SDK calls marshaled to native thread pool (handled by sdk-bridge)
- Flow emissions collected on `Dispatchers.Main` in ViewModel

---

## Testing Contract

### Mock Implementations Required

```kotlin
// For unit tests:
class MockQualityProfileRepository : QualityProfileRepository {
    var getProfilesReturn = listOf(QualityProfile.Smooth)
    var getPreferenceReturn = QualityPreference()
    
    override suspend fun getAllProfiles() = getProfilesReturn
    override suspend fun getQualityPreference(sourceId: String?) = getPreferenceReturn
    override suspend fun setQualityPreference(preference: QualityPreference) { }
    override suspend fun clearPreferences() { }
}
```

### Integration Test Scenarios

1. **Test:** Load quality preferences, user selects "Balanced", verify saved
2. **Test:** Start playback with "Smooth", verify applyQualityProfile called with H.264
3. **Test:** Frame drops spike to 12%, verify degradeQuality called
4. **Test:** Stream disconnects, verify handleStreamDisconnection retry loop with backoff
5. **Test:** Reconnection succeeds on 2nd attempt, verify quality restored

---

## Versioning & Deprecation

**Version**: 1.0 (Initial)

**Planned Additions** (Phase 2 - Future):

```kotlin
// Future: Per-network-type profiles (WiFi vs Cellular)
fun getQualityPreferenceForNetworkType(networkType: NetworkType): QualityPreference

// Future: Device capability detection (CPU class, available RAM)
fun recommendProfileForDevice(): QualityProfile

// Future: Quality profile analytics (most-used profiles, degradation frequency)
fun getQualityAnalytics(timeWindow: Duration): QualityAnalytics
```

---

## Conclusion

This contract establishes clear ownership and responsibility boundaries:
- **QualityProfileRepository**: Manages profile selection and persistence
- **NdiViewerRepository**: Applies profiles to NDI stream and monitors playback metrics
- **PlayerScalingCalculator**: Calculates layout dimensions for UI rendering

All methods are suspension-safe, error-handled, and testable. Thread safety and offline resilience are built in. Ready for Phase 2 task decomposition and implementation.
