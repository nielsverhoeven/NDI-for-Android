package com.ndi.feature.ndibrowser.viewer

interface PlayerScalingCalculator {
    fun calculateScaledDimensions(state: PlayerScalingState): ScaledDimensions

    fun calculateLetterboxGeometry(
        state: PlayerScalingState,
        scaledDimensions: ScaledDimensions,
    ): LetterboxGeometry

    fun meetsUtilizationTarget(state: PlayerScalingState): Boolean

    fun requiresRecalculation(oldState: PlayerScalingState?, newState: PlayerScalingState): Boolean
}
