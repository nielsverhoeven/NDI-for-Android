# Data Model: NDI Stream Playback Optimization

**Phase**: 1 (Design - define entities, relationships, state management)  
**Date**: March 28, 2026

---

## Overview

This document defines the data entities and state management structures required for implementing smooth NDI stream playback with user-adjustable quality controls. The model follows MVVM architecture with Repository-mediated access per project constitution.

---

## Core Entities

### 1. QualityProfile (Value Object)

Represents a preset collection of playback optimization parameters.

```kotlin
// feature/ndi-browser/domain/repository/QualityProfile.kt
sealed class QualityProfile(
    val id: String,                    // "smooth", "balanced", "high_quality"
    val displayName: String,           // "Smooth", "Balanced", "High Quality"
    val description: String,           // "Best for slow networks", "Adaptive", "Maximize detail"
    val baseResolution: Resolution,    // 720p, 1080p, 1440p
    val maxFrameRate: Int,            // 30, 60
    val preferredCodec: Codec,        // H.264, H.265
    val targetBitrate: Int,           // Mbps (informational)
    val priority: Int                 // Lower = downgrade first
) {
    object Smooth : QualityProfile(
        id = "smooth",
        displayName = "Smooth",
        description = "Best for slow networks",
        baseResolution = Resolution(720, 480),
        maxFrameRate = 30,
        preferredCodec = Codec.H264,
        targetBitrate = 2,
        priority = 1
    )
    
    object Balanced : QualityProfile(
        id = "balanced",
        displayName = "Balanced", 
        description = "Adaptive quality",
        baseResolution = Resolution(1080, 720),
        maxFrameRate = 30,
        preferredCodec = Codec.H264,
        targetBitrate = 5,
        priority = 2
    )
    
    object HighQuality : QualityProfile(
        id = "high_quality",
        displayName = "High Quality",
        description = "Maximize detail",
        baseResolution = Resolution(1440, 1080),
        maxFrameRate = 60,
        preferredCodec = Codec.H265,
        targetBitrate = 10,
        priority = 3
    )
}

enum class Codec {
    H264, H265, VP9
}

data class Resolution(
    val width: Int,
    val height: Int
) {
    val aspectRatio: Float get() = width.toFloat() / height
}
```

---

### 2. PlaybackOptimization (State Object)

Encapsulates real-time playback quality state and monitoring.

```kotlin
// feature/ndi-browser/data/model/PlaybackOptimization.kt
data class PlaybackOptimization(
    val sourceId: String,                          // NDI source being played
    val selectedProfile: QualityProfile,           // User's active preset
    val currentResolution: Resolution,             // Actual resolution streaming
    val currentFrameRate: Int,                     // Actual fps achieved
    val currentCodec: Codec,                       // Actual codec applied
    val bufferHealthPercent: Int,                  // 0-100, buffered data %
    val frameDropPercentage: Float,                // Last 1-sec window: dropped frames %
    val isAutoOptimizing: Boolean,                 // True if system auto-downgrading on drops
    val lastQualityChangeTime: Long,               // Timestamp of last adjustment
    val estimatedBitrate: Int = 0,                // Mbps inferred from frame rate
    val timestamp: Long = System.currentTimeMillis()
) {
    
    // Compute whether quality should be degraded based on frame drops
    fun shouldDowngradeQuality(): Boolean {
        val frameDropThreshold = selectedProfile.getFrameDropThreshold()
        return frameDropPercentage > frameDropThreshold
    }
    
    // Compute next lower-priority profile
    fun getDowngradeProfile(): QualityProfile? {
        return listOf(QualityProfile.Smooth, QualityProfile.Balanced, QualityProfile.HighQuality)
            .sorted()
            .firstOrNull { it.priority < selectedProfile.priority }
    }
}

// Extension function for threshold config
private fun QualityProfile.getFrameDropThreshold(): Float {
    return when (this) {
        is QualityProfile.Smooth -> 5f
        is QualityProfile.Balanced -> 10f
        is QualityProfile.HighQuality -> 15f
    }
}
```

---

### 3. PlayerScalingState (Layout Model)

Manages player area dimensions and scaling calculations for auto-fit.

```kotlin
// feature/ndi-browser/presentation/viewer/PlayerScalingState.kt
data class PlayerScalingState(
    val availableWidth: Int,                      // Player container width (dp)
    val availableHeight: Int,                     // Player container height (dp)
    val streamAspectRatio: Float,                 // Stream native aspect (W/H)
    val orientation: Orientation = Orientation.PORTRAIT
) {
    
    enum class Orientation {
        PORTRAIT, LANDSCAPE
    }
    
    // Calculate actual video dimensions that fit within bounds while preserving aspect
    fun calculateScaledDimensions(): ScaledDimensions {
        val availableAspect = availableWidth.toFloat() / availableHeight
        
        return if (streamAspectRatio > availableAspect) {
            // Stream wider than bounds -> letterbox top/bottom
            ScaledDimensions(
                width = availableWidth,
                height = (availableWidth / streamAspectRatio).toInt(),
                letterboxSide = LetterboxSide.TOP_BOTTOM
            )
        } else {
            // Stream taller than bounds -> letterbox left/right
            ScaledDimensions(
                width = (availableHeight * streamAspectRatio).toInt(),
                height = availableHeight,
                letterboxSide = LetterboxSide.LEFT_RIGHT
            )
        }
    }
    
    // Verify that scaled content fills at least 90% of available space
    fun meetsUtilizationTarget(): Boolean {
        val scaled = calculateScaledDimensions()
        val utilization = (scaled.width * scaled.height).toFloat() / (availableWidth * availableHeight)
        return utilization >= 0.9f
    }
}

data class ScaledDimensions(
    val width: Int,
    val height: Int,
    val letterboxSide: LetterboxSide
)

enum class LetterboxSide {
    TOP_BOTTOM, LEFT_RIGHT, NONE
}
```

---

### 4. QualityPreference (Persistence Model)

User's stored quality preset choice (persisted in SharedPreferences).

```kotlin
// feature/ndi-browser/data/model/QualityPreference.kt
data class QualityPreference(
    val sourceId: String? = null,                // Null = global default; source ID = per-source override
    val selectedProfileId: String = "smooth",   // Default to smooth
    val timestamp: Long = System.currentTimeMillis()
) {
    
    fun toQualityProfile(): QualityProfile {
        return when (selectedProfileId) {
            "smooth" -> QualityProfile.Smooth
            "balanced" -> QualityProfile.Balanced
            "high_quality" -> QualityProfile.HighQuality
            else -> QualityProfile.Smooth  // Default fallback
        }
    }
}
```

---

### 5. DisconnectionEvent (Event Model)

Encapsulates stream disconnection events for UI recovery flow.

```kotlin
// feature/ndi-browser/data/model/DisconnectionEvent.kt
sealed class DisconnectionEvent {
    
    data class StreamDisconnected(
        val sourceId: String,
        val sourceName: String,
        val reason: String,
        val timestamp: Long = System.currentTimeMillis()
    ) : DisconnectionEvent()
    
    data class ReconnectionAttempt(
        val attemptNumber: Int,
        val maxRetries: Int = 5
    ) : DisconnectionEvent()
    
    data class ReconnectionSuccess(
        val totalAttemptsNeeded: Int
    ) : DisconnectionEvent()
    
    data class ReconnectionFailed(
        val lastError: String
    ) : DisconnectionEvent()
}
```

---

## State Management

### PlaybackOptimizationViewModel State Flow

```kotlin
// Pseudo-code showing state flow integration with ViewModel
class ViewerViewModel(
    private val ndiRepository: NdiViewerRepository,
    private val qualityRepository: QualityProfileRepository
) : ViewModel() {
    
    // Quality/optimization state exposed to UI
    private val _playbackOptimization = MutableStateFlow<PlaybackOptimization?>(null)
    val playbackOptimization: StateFlow<PlaybackOptimization?> = _playbackOptimization.asStateFlow()
    
    // Player scaling state for layout calculations
    private val _playerScaling = MutableStateFlow<PlayerScalingState?>(null)
    val playerScaling: StateFlow<PlayerScalingState?> = _playerScaling.asStateFlow()
    
    // Disconnection recovery state
    private val _disconnectionEvent = MutableSharedFlow<DisconnectionEvent>()
    val disconnectionEvent: SharedFlow<DisconnectionEvent> = _disconnectionEvent.asSharedFlow()
    
    // User's current quality preference
    private val _qualityPreference = MutableStateFlow(QualityPreference())
    val qualityPreference: StateFlow<QualityPreference> = _qualityPreference.asStateFlow()
    
    // Frame monitoring coroutine (collects frame rate data from NDI SDK)
    private val frameMonitoringJob: Job? = null
}
```

---

## Data Access & Persistence

### SharedPreferencesQualityStore (Local Storage Wrapper)

```kotlin
// feature/ndi-browser/data/local/SharedPreferencesQualityStore.kt
class SharedPreferencesQualityStore(
    private val context: Context
) {
    private val prefs = context.getSharedPreferences(
        "ndi_quality_prefs",
        Context.MODE_PRIVATE
    )
    
    fun saveQualityPreference(preference: QualityPreference) {
        prefs.edit {
            putString("quality_profile_id", preference.selectedProfileId)
            if (preference.sourceId != null) {
                putString("source_id_${preference.sourceId}", preference.selectedProfileId)
            }
        }
    }
    
    fun loadQualityPreference(sourceId: String? = null): QualityPreference {
        val profileId = if (sourceId != null) {
            prefs.getString("source_id_$sourceId", null) 
                ?: prefs.getString("quality_profile_id", "smooth")
        } else {
            prefs.getString("quality_profile_id", "smooth") ?: "smooth"
        }
        return QualityPreference(sourceId, profileId)
    }
}
```

### QualityProfileRepository (Data Access)

```kotlin
// feature/ndi-browser/data/repository/QualityProfileRepository.kt
interface QualityProfile Repository {
    suspend fun getQualityPreference(sourceId: String? = null): QualityPreference
    suspend fun setQualityPreference(preference: QualityPreference)
    suspend fun getAllProfiles(): List<QualityProfile>
}

class QualityProfileRepositoryImpl(
    private val store: SharedPreferencesQualityStore
) : QualityProfileRepository {
    
    override suspend fun getQualityPreference(sourceId: String?): QualityPreference {
        return withContext(Dispatchers.IO) {
            store.loadQualityPreference(sourceId)
        }
    }
    
    override suspend fun setQualityPreference(preference: QualityPreference) {
        withContext(Dispatchers.IO) {
            store.saveQualityPreference(preference)
        }
    }
    
    override suspend fun getAllProfiles(): List<QualityProfile> {
        return listOf(
            QualityProfile.Smooth,
            QualityProfile.Balanced,
            QualityProfile.HighQuality
        )
    }
}
```

---

## Relationship Diagram

```
┌─────────────────────┐
│  PlaybackOptimization
│  - selectedProfile ──────────→ QualityProfile (value object)
│  - currentResolution ────────→ Resolution
│  - currentCodec ────────────→ Codec (enum)
└─────────────────────┘

┌─────────────────────┐
│  PlayerScalingState
│  - streamAspectRatio ────────┐
│  - calculateScaleDimensions → ScaledDimensions
└─────────────────────┘

┌─────────────────────┐
│  QualityPreference (persisted)
│  - selectedProfileId ─────────┐
│                         (stored in SharedPreferences)
└─────────────────────┘

┌─────────────────────┐
│  DisconnectionEvent (UI event stream)
└─────────────────────┘
```

---

## Validation Rules

### QualityProfile Constraints

- Resolution must be valid (width > 0, height > 0)
- Frame rate must be 24, 30, or 60 fps
- Bitrate estimate must be positive
- Priority values must be unique across presets

### PlaybackOptimization Constraints

- frameDropPercentage >= 0 and <= 100
- bufferHealthPercent >= 0 and <= 100
- currentFrameRate must match selectedProfile.maxFrameRate or lower
- Timestamp must be recent (< 5 seconds old for active playback)

### PlayerScalingState Constraints

- availableWidth > 0, availableHeight > 0
- streamAspectRatio > 0
- Scaled dimensions must fit within available bounds

### QualityPreference Constraints

- selectedProfileId must be one of: "smooth", "balanced", "high_quality"
- sourceId can be null (for global default) or valid NDI source ID

---

## Migration & Compatibility

### SharedPreferences Schema

**Current** (forward-compatible):
```
Key: "quality_profile_id" → Value: "smooth"|"balanced"|"high_quality"
Key: "source_id_{sourceId}" → Value: "smooth"|"balanced"|"high_quality"
```

**Future Upgrade Path** (if migrating to Room):
- Create QualityPreferenceEntity table
- Run one-time migration from SharedPreferences → Room on first app launch
- Maintain SharedPreferences key for backward compatibility during transition window

---

## Conclusion

The data model establishes clear, testable entities for:
- **QualityProfile**: Immutable presets defining codec/resolution targets
- **PlaybackOptimization**: Mutable state tracking real-time performance and quality decisions
- **PlayerScalingState**: Layout calculation model for aspect-ratio-preserving scaling
- **QualityPreference**: User preference persistence via SharedPreferences
- **DisconnectionEvent**: Event-based recovery flow

All entities follow value object and domain-driven design patterns. Repository pattern and SharedPreferences storage align with project architecture constraints. Ready for Phase 1 contract definitions and Phase 2 task decomposition.
