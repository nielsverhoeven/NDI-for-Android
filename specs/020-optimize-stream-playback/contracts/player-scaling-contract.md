# API Contract: Player Scaling & Layout

**Phase**: 1 (Design - define public interfaces)  
**Date**: March 28, 2026  
**Module**: feature/ndi-browser/presentation

---

## Overview

This contract defines the public interface for aspect-ratio-preserving video scaling and player layout calculations. It ensures that the video content fills the available player bounds while maintaining native aspect ratio without distortion.

---

## Requirement Foundation

**US-2 (Auto-fit Player)**: NDI player automatically scales video to fit player area without distortion, preserving aspect ratio.

**Success Criteria**:
- Scaled video fills ≥90% of available player area
- No visible distortion or stretching of video content
- Layout adjusts correctly on device rotation (portrait ↔ landscape)
- Letterbox/pillar bars compute correctly for all stream aspect ratios

---

## Interface: PlayerScalingCalculator

**Location**: `feature/ndi-browser/presentation/viewer/PlayerScalingCalculator.kt`

**Responsibility**: Calculate video output dimensions and letterbox positioning.

```kotlin
interface PlayerScalingCalculator {
    
    /**
     * Calculate the scaled dimensions for video content to fit within bounds
     * while preserving aspect ratio.
     * 
     * ALGORITHM (Aspect-Ratio-Preserving Fit):
     * ┌─ Measure container aspect ratio: containerW / containerH
     * ├─ Measure stream aspect ratio: streamW / streamH
     * ├─ IF stream aspect > container aspect:
     * │  └─ Scale to container width, letterbox top/bottom
     * ├─ ELSE IF stream aspect < container aspect:
     * │  └─ Scale to container height, letterbox left/right
     * └─ ELSE (equal aspects)
     *    └─ Fill entire bounds, no letterbox
     * 
     * Result verification:
     * - utilization = (scaledW * scaledH) / (containerW * containerH)
     * - Assert utilization >= 0.90 (catch degenerate cases)
     * 
     * @param state PlayerScalingState with:
     *              - availableWidth: Player container width (dp)
     *              - availableHeight: Player container height (dp)
     *              - streamAspectRatio: Stream native W/H
     *              - orientation: PORTRAIT or LANDSCAPE
     * 
     * @return ScaledDimensions {
     *           width: Actual video output width (dp)
     *           height: Actual video output height (dp)
     *           letterboxSide: TOP_BOTTOM, LEFT_RIGHT, or NONE
     *         }
     * 
     * @throws IllegalArgumentException if state dimensions invalid
     *         (availableWidth <= 0, availableHeight <= 0, streamAspectRatio <= 0)
     */
    fun calculateScaledDimensions(state: PlayerScalingState): ScaledDimensions
    
    /**
     * Calculate the position and size of letterbox bars (dark overlays).
     * 
     * GEOMETRY:
     * - If letterboxSide == TOP_BOTTOM:
     *   ├─ Top bar: x=0, y=0, w=containerW, h=(containerH - scaledH)/2
     *   ├─ Bottom bar: x=0, y=scaledH+(h/2), w=containerW, h=(containerH - scaledH)/2
     *   └─ Video: x=(containerW-scaledW)/2, y=(containerH-scaledH)/2
     * 
     * - If letterboxSide == LEFT_RIGHT:
     *   ├─ Left bar: x=0, y=0, w=(containerW-scaledW)/2, h=containerH
     *   ├─ Right bar: x=scaledW+(w/2), y=0, w=(containerW-scaledW)/2, h=containerH
     *   └─ Video: x=(containerW-scaledW)/2, y=(containerH-scaledH)/2
     * 
     * - If letterboxSide == NONE:
     *   └─ Video fills entire container, no bars needed
     * 
     * @param state PlayerScalingState
     * @param scaledDimensions Result from calculateScaledDimensions()
     * 
     * @return LetterboxGeometry {
     *           topBar: Rect? (null if not needed)
     *           bottomBar: Rect? (null if not needed)
     *           leftBar: Rect? (null if not needed)
     *           rightBar: Rect? (null if not needed)
     *           videoRect: Rect (centered within container)
     *         }
     */
    fun calculateLetterboxGeometry(
        state: PlayerScalingState,
        scaledDimensions: ScaledDimensions
    ): LetterboxGeometry
    
    /**
     * Verify that calculated scaling meets minimum space utilization target.
     * 
     * Utilization = (scaledW * scaledH) / (containerW * containerH)
     * Target: utilization >= 0.90
     * 
     * If utilization < 0.90, indicates a degenerate aspect ratio mismatch
     * that may warrant fallback or error handling.
     * 
     * @param state PlayerScalingState
     * @return true if utilization >= 0.90, false otherwise
     */
    fun meetsUtilizationTarget(state: PlayerScalingState): Boolean
    
    /**
     * Detect if layout needs recalculation after orientation change.
     * 
     * Called by ViewModel when:
     * - Device rotates (onConfigurationChanged)
     * - Player resized (fragment view size changed)
     * - Stream aspect ratio changes (detected from metadata)
     * 
     * Compares new state against previous calculation. If any dimension
     * or aspect ratio changed significantly (>2%), returns true.
     * 
     * @param oldState Previous PlayerScalingState
     * @param newState New PlayerScalingState
     * @return true if recalculation required
     */
    fun requiresRecalculation(
        oldState: PlayerScalingState?,
        newState: PlayerScalingState
    ): Boolean
}
```

---

## Data Types

### ScaledDimensions

```kotlin
data class ScaledDimensions(
    val width: Int,                        // Output width (dp)
    val height: Int,                       // Output height (dp)
    val letterboxSide: LetterboxSide,      // Where letterbox appears
    val utilization: Float = 0f            // Percentage of container filled (0.0-1.0)
) {
    init {
        require(width > 0 && height > 0) { "Dimensions must be positive" }
        require(utilization in 0f..1f) { "Utilization must be 0-1" }
    }
}

enum class LetterboxSide {
    TOP_BOTTOM,    // Horizontal stream in vertical container
    LEFT_RIGHT,    // Vertical stream in horizontal container
    NONE           // Stream aspect matches container
}
```

### LetterboxGeometry

```kotlin
data class LetterboxGeometry(
    val topBar: Rect? = null,              // Top letterbox bounds (if TOP_BOTTOM)
    val bottomBar: Rect? = null,           // Bottom letterbox bounds (if TOP_BOTTOM)
    val leftBar: Rect? = null,             // Left letterbox bounds (if LEFT_RIGHT)
    val rightBar: Rect? = null,            // Right letterbox bounds (if LEFT_RIGHT)
    val videoRect: Rect                    // Centered video output bounds
) {
    fun getLetterboxRects(): List<Rect> {
        return listOfNotNull(topBar, bottomBar, leftBar, rightBar)
    }
    
    fun getTotalLetterboxArea(): Int {
        return getLetterboxRects().sumOf { it.width * it.height }
    }
}

data class Rect(
    val x: Int,
    val y: Int,
    val width: Int,
    val height: Int
)
```

---

## ViewModel Integration

### PlayerScalingViewModel

```kotlin
class PlayerScalingViewModel(
    private val calculator: PlayerScalingCalculator
) : ViewModel() {
    
    // Current scaling state
    private val _scalingState = MutableStateFlow<PlayerScalingState?>(null)
    val scalingState: StateFlow<PlayerScalingState?> = _scalingState.asStateFlow()
    
    // Calculated dimensions for Compose layout
    private val _scaledDimensions = MutableStateFlow<ScaledDimensions?>(null)
    val scaledDimensions: StateFlow<ScaledDimensions?> = _scaledDimensions.asStateFlow()
    
    // Letterbox bar positions for rendering
    private val _letterboxGeometry = MutableStateFlow<LetterboxGeometry?>(null)
    val letterboxGeometry: StateFlow<LetterboxGeometry?> = _letterboxGeometry.asStateFlow()
    
    // Called from Compose layout when measuring available space
    fun updatePlayerBounds(width: Int, height: Int, streamAspectRatio: Float) {
        val newState = PlayerScalingState(
            availableWidth = width,
            availableHeight = height,
            streamAspectRatio = streamAspectRatio,
            orientation = getOrientation()
        )
        
        if (calculator.requiresRecalculation(_scalingState.value, newState)) {
            _scalingState.value = newState
            _scaledDimensions.value = calculator.calculateScaledDimensions(newState)
            _letterboxGeometry.value = _scaledDimensions.value?.let {
                calculator.calculateLetterboxGeometry(newState, it)
            }
        }
    }
    
    // Called on device orientation change
    fun onOrientationChanged(newOrientation: Orientation) {
        _scalingState.value?.let { oldState ->
            val newState = oldState.copy(orientation = newOrientation)
            updatePlayerBounds(newState.availableWidth, newState.availableHeight, newState.streamAspectRatio)
        }
    }
}
```

---

## Rendering Contract (UI Layer)

### Jetpack Compose Layout

```kotlin
@Composable
fun PlayerSurfaceLayout(
    scaledDimensions: ScaledDimensions,
    letterboxGeometry: LetterboxGeometry,
    modifier: Modifier = Modifier
) {
    Box(
        modifier = modifier
            .fillMaxSize()
            .background(Color.Black)  // Default letterbox color
    ) {
        // Render letterbox bars
        letterboxGeometry.topBar?.let { rect ->
            Box(
                modifier = Modifier
                    .offset(x = rect.x.dp, y = rect.y.dp)
                    .size(width = rect.width.dp, height = rect.height.dp)
                    .background(Color.Black)
            )
        }
        // ... similar for bottom, left, right bars ...
        
        // Render video surface (SurfaceView or TextureView)
        NdiVideoSurface(
            modifier = Modifier
                .offset(
                    x = letterboxGeometry.videoRect.x.dp,
                    y = letterboxGeometry.videoRect.y.dp
                )
                .size(
                    width = scaledDimensions.width.dp,
                    height = scaledDimensions.height.dp
                )
        )
    }
}
```

---

## Testing Contract

### Unit Tests

```kotlin
// Test 1: Landscape stream in portrait container
@Test
fun testWideStreamNarrowContainer() {
    val state = PlayerScalingState(
        availableWidth = 1080,          // Portrait width
        availableHeight = 2340,         // Portrait height (tall)
        streamAspectRatio = 16f / 9f,   // Landscape (1.78)
        orientation = Orientation.PORTRAIT
    )
    
    val scaled = calculator.calculateScaledDimensions(state)
    
    assertEquals(1080, scaled.width)  // Scaled to container width
    assertEquals(608, scaled.height)  // 1080 / (16/9)
    assertEquals(LetterboxSide.TOP_BOTTOM, scaled.letterboxSide)
    assertTrue(calculator.meetsUtilizationTarget(state))
}

// Test 2: Portrait stream in landscape container
@Test
fun testPortraitStreamWideContainer() {
    val state = PlayerScalingState(
        availableWidth = 2340,          // Landscape width (wide)
        availableHeight = 1080,         // Landscape height
        streamAspectRatio = 9f / 16f,   // Portrait (0.56)
        orientation = Orientation.LANDSCAPE
    )
    
    val scaled = calculator.calculateScaledDimensions(state)
    
    assertEquals(607, scaled.width)   // 1080 × (9/16)
    assertEquals(1080, scaled.height) // Scaled to container height
    assertEquals(LetterboxSide.LEFT_RIGHT, scaled.letterboxSide)
    assertTrue(calculator.meetsUtilizationTarget(state))
}

// Test 3: Matching aspect ratios (no letterbox)
@Test
fun testMatchingAspectRatios() {
    val state = PlayerScalingState(
        availableWidth = 1920,
        availableHeight = 1080,
        streamAspectRatio = 16f / 9f,
        orientation = Orientation.LANDSCAPE
    )
    
    val scaled = calculator.calculateScaledDimensions(state)
    
    assertEquals(1920, scaled.width)
    assertEquals(1080, scaled.height)
    assertEquals(LetterboxSide.NONE, scaled.letterboxSide)
    assertEquals(1f, scaled.utilization)
}

// Test 4: Orientation change detection
@Test
fun testOrientationChangeRequiresRecalculation() {
    val portrait = PlayerScalingState(1080, 2340, 16f/9f, Orientation.PORTRAIT)
    val landscape = PlayerScalingState(2340, 1080, 16f/9f, Orientation.LANDSCAPE)
    
    assertTrue(calculator.requiresRecalculation(portrait, landscape))
}
```

### Integration Tests (e2e)

```kotlin
// Test: Real NDI stream rendering with auto-fit
@Test
fun testAutoFitWithRealNdiStream() {
    val activity = activityRule.activity
    val viewModel = PlayerScalingViewModel(RealPlayerScalingCalculator())
    
    // Start NDI stream playback (emulator)
    val stream = connectToTestNdiSource()
    val streamAspect = stream.getWidth() / stream.getHeight()
    
    // Measure player bounds
    val playerView = activity.findViewById<View>(R.id.player_surface)
    val bounds = IntArray(2)
    playerView.getLocationOnScreen(bounds)
    
    // Update ViewModel with bounds and aspect ratio
    viewModel.updatePlayerBounds(
        width = playerView.width,
        height = playerView.height,
        streamAspectRatio = streamAspect
    )
    
    // Verify scaled dimensions in UI (screenshot comparison)
    assertThat(viewModel.scaledDimensions.value?.utilization).isAtLeast(0.9f)
    assertFalse(screenHasDistortedVideo())  // Visual verification
}
```

---

## Error Handling

### Error Cases

| Scenario | Input | Exception | Recovery |
|----------|-------|-----------|----------|
| Invalid bounds | width=0, height=100 | IllegalArgumentException | Caller must validate |
| Invalid aspect ratio | streamAspect=0 | IllegalArgumentException | Caller must validate |
| Extreme aspect ratio | streamAspect=0.01 (4:400) | IllegalArgumentException (if utilization < 0.90) | Fall back to default profile |
| Rotation during calculation | State changes mid-computation | (None - pure function) | Callers retry with new state |

### Contract Guarantee

**Pure Function Guarantee**: PlayerScalingCalculator methods are **pure** (no side effects, deterministic).
Given identical inputs, always produce identical outputs. Safe to call multiple times.

---

## Performance Contract

**Computational Budget**: 
- `calculateScaledDimensions()`: < 1ms (simple arithmetic)
- `calculateLetterboxGeometry()`: < 1ms (position calculations)
- `requiresRecalculation()`: < 1ms (comparison)

**Recommended Call Frequency**:
- On device rotation: Once (in onConfigurationChanged)
- On view resize: Once (in onMeasure callback)
- On stream connect/disconnect: Once (when aspect ratio known)
- During playback: **Not called** (dimensions static)

---

## Accessibility Contract

**Semantics for Compose**:

```kotlin
NdiVideoSurface(
    modifier = Modifier
        .semantics {
            contentDescription = "NDI Video Stream (${streamTitle})"
            role = Role.Image
        }
)
```

**No Interactive Elements on Video**: Player scaling calculations do NOT affect tap targets or touch gesture zones. Scaling is **layout-only** (no semantic changes to interaction model).

---

## Versioning

**Version**: 1.0 (Initial)

**Stability**: Stable (no planned breaking changes)

**Deprecated Methods**: None

**Future Extensibility**:

```kotlin
// Phase 2 consideration: Support custom aspect ratios from metadata
fun calculateScaledDimensionsWithMetadata(
    state: PlayerScalingState,
    streamMetadata: NdiStreamMetadata
): ScaledDimensions

// Phase 2 consideration: Fit mode selection (CONTAIN, COVER, FILL_XY, etc.)
fun calculateScaledDimensionsWithFitMode(
    state: PlayerScalingState,
    fitMode: FitMode
): ScaledDimensions
```

---

## Conclusion

This contract establishes:
- **Clear responsibility**: PlayerScalingCalculator owns all scaling math (pure functions)
- **Testable interface**: Well-defined inputs/outputs with comprehensive test coverage
- **ViewModel integration**: State flow pattern for Compose recomposition
- **Rendering guarantee**: Correct letterbox geometry for all aspect ratios
- **Performance budget**: All calculations complete in <1ms
- **Accessibility**: Semantics preserved, no interactive elements on video

Ready for Phase 2 task decomposition and implementation of concrete calculator classes.
