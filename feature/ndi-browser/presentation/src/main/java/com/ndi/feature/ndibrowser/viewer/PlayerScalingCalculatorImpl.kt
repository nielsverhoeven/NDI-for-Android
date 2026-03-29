package com.ndi.feature.ndibrowser.viewer

class PlayerScalingCalculatorImpl : PlayerScalingCalculator {
    override fun calculateScaledDimensions(state: PlayerScalingState): ScaledDimensions {
        require(state.availableWidth > 0) { "availableWidth must be positive" }
        require(state.availableHeight > 0) { "availableHeight must be positive" }
        require(state.streamAspectRatio > 0f) { "streamAspectRatio must be positive" }

        val availableAspect = state.availableWidth.toFloat() / state.availableHeight.toFloat()
        return if (state.streamAspectRatio > availableAspect) {
            val scaledHeight = (state.availableWidth / state.streamAspectRatio).toInt().coerceAtLeast(1)
            val utilization = (state.availableWidth * scaledHeight).toFloat() /
                (state.availableWidth * state.availableHeight).toFloat()
            ScaledDimensions(
                width = state.availableWidth,
                height = scaledHeight,
                letterboxSide = LetterboxSide.TOP_BOTTOM,
                utilization = utilization,
            )
        } else if (state.streamAspectRatio < availableAspect) {
            val scaledWidth = (state.availableHeight * state.streamAspectRatio).toInt().coerceAtLeast(1)
            val utilization = (scaledWidth * state.availableHeight).toFloat() /
                (state.availableWidth * state.availableHeight).toFloat()
            ScaledDimensions(
                width = scaledWidth,
                height = state.availableHeight,
                letterboxSide = LetterboxSide.LEFT_RIGHT,
                utilization = utilization,
            )
        } else {
            ScaledDimensions(
                width = state.availableWidth,
                height = state.availableHeight,
                letterboxSide = LetterboxSide.NONE,
                utilization = 1f,
            )
        }
    }

    override fun calculateLetterboxGeometry(
        state: PlayerScalingState,
        scaledDimensions: ScaledDimensions,
    ): LetterboxGeometry {
        val horizontalPadding = ((state.availableWidth - scaledDimensions.width) / 2).coerceAtLeast(0)
        val verticalPadding = ((state.availableHeight - scaledDimensions.height) / 2).coerceAtLeast(0)
        val videoRect = Rect(
            x = horizontalPadding,
            y = verticalPadding,
            width = scaledDimensions.width,
            height = scaledDimensions.height,
        )
        return when (scaledDimensions.letterboxSide) {
            LetterboxSide.TOP_BOTTOM -> LetterboxGeometry(
                topBar = Rect(0, 0, state.availableWidth, verticalPadding),
                bottomBar = Rect(0, verticalPadding + scaledDimensions.height, state.availableWidth, verticalPadding),
                videoRect = videoRect,
            )
            LetterboxSide.LEFT_RIGHT -> LetterboxGeometry(
                leftBar = Rect(0, 0, horizontalPadding, state.availableHeight),
                rightBar = Rect(horizontalPadding + scaledDimensions.width, 0, horizontalPadding, state.availableHeight),
                videoRect = videoRect,
            )
            LetterboxSide.NONE -> LetterboxGeometry(videoRect = videoRect)
        }
    }

    override fun meetsUtilizationTarget(state: PlayerScalingState): Boolean {
        return calculateScaledDimensions(state).utilization >= 0.9f
    }

    override fun requiresRecalculation(oldState: PlayerScalingState?, newState: PlayerScalingState): Boolean {
        if (oldState == null) return true
        if (oldState.orientation != newState.orientation) return true
        if (oldState.availableWidth != newState.availableWidth) return true
        if (oldState.availableHeight != newState.availableHeight) return true
        val delta = kotlin.math.abs(oldState.streamAspectRatio - newState.streamAspectRatio)
        return delta > 0.02f
    }
}
