package com.ndi.feature.ndibrowser.viewer

import androidx.lifecycle.ViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

class PlayerScalingViewModel(
    private val calculator: PlayerScalingCalculator = PlayerScalingCalculatorImpl(),
) : ViewModel() {

    private val _scalingState = MutableStateFlow<PlayerScalingState?>(null)
    val scalingState: StateFlow<PlayerScalingState?> = _scalingState.asStateFlow()

    private val _scaledDimensions = MutableStateFlow<ScaledDimensions?>(null)
    val scaledDimensions: StateFlow<ScaledDimensions?> = _scaledDimensions.asStateFlow()

    private val _letterboxGeometry = MutableStateFlow<LetterboxGeometry?>(null)
    val letterboxGeometry: StateFlow<LetterboxGeometry?> = _letterboxGeometry.asStateFlow()

    fun updatePlayerBounds(width: Int, height: Int, streamAspectRatio: Float, orientation: Orientation) {
        val nextState = PlayerScalingState(
            availableWidth = width,
            availableHeight = height,
            streamAspectRatio = streamAspectRatio,
            orientation = orientation,
        )
        if (!calculator.requiresRecalculation(_scalingState.value, nextState)) {
            return
        }
        _scalingState.value = nextState
        val scaled = calculator.calculateScaledDimensions(nextState)
        _scaledDimensions.value = scaled
        _letterboxGeometry.value = calculator.calculateLetterboxGeometry(nextState, scaled)
    }
}
