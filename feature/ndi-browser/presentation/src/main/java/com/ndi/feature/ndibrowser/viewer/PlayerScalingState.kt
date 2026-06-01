package com.ndi.feature.ndibrowser.viewer

data class PlayerScalingState(
    val availableWidth: Int,
    val availableHeight: Int,
    val streamAspectRatio: Float,
    val orientation: Orientation,
)

enum class Orientation {
    PORTRAIT,
    LANDSCAPE,
}

enum class LetterboxSide {
    TOP_BOTTOM,
    LEFT_RIGHT,
    NONE,
}

data class ScaledDimensions(
    val width: Int,
    val height: Int,
    val letterboxSide: LetterboxSide,
    val utilization: Float,
)

data class Rect(
    val x: Int,
    val y: Int,
    val width: Int,
    val height: Int,
)

data class LetterboxGeometry(
    val topBar: Rect? = null,
    val bottomBar: Rect? = null,
    val leftBar: Rect? = null,
    val rightBar: Rect? = null,
    val videoRect: Rect,
)
