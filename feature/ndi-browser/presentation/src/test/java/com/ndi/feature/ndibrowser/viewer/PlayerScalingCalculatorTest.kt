package com.ndi.feature.ndibrowser.viewer

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class PlayerScalingCalculatorTest {

    private val calculator: PlayerScalingCalculator = PlayerScalingCalculatorImpl()

    @Test
    fun stream16x9_in1000x600_scalesToExpectedContract() {
        val state = PlayerScalingState(
            availableWidth = 1000,
            availableHeight = 600,
            streamAspectRatio = 16f / 9f,
            orientation = Orientation.LANDSCAPE,
        )

        val scaled = calculator.calculateScaledDimensions(state)

        assertEquals(1000, scaled.width)
        assertEquals(562, scaled.height)
        assertEquals(LetterboxSide.TOP_BOTTOM, scaled.letterboxSide)
        assertTrue(scaled.utilization in 0.93f..0.94f)
    }

    @Test
    fun stream4x3_in1000x600_matchesLetterboxContract() {
        val state = PlayerScalingState(
            availableWidth = 1000,
            availableHeight = 600,
            streamAspectRatio = 4f / 3f,
            orientation = Orientation.LANDSCAPE,
        )

        val scaled = calculator.calculateScaledDimensions(state)

        assertEquals(800, scaled.width)
        assertEquals(600, scaled.height)
        assertEquals(LetterboxSide.LEFT_RIGHT, scaled.letterboxSide)
        assertTrue(scaled.utilization in 0.79f..0.81f)
    }

    @Test
    fun stream21x9_inLandscape_matchesPillarboxContract() {
        val state = PlayerScalingState(
            availableWidth = 1000,
            availableHeight = 600,
            streamAspectRatio = 21f / 9f,
            orientation = Orientation.LANDSCAPE,
        )

        val scaled = calculator.calculateScaledDimensions(state)
        assertEquals(1000, scaled.width)
        assertEquals(428, scaled.height)
        assertEquals(LetterboxSide.TOP_BOTTOM, scaled.letterboxSide)
        assertTrue(scaled.utilization in 0.71f..0.72f)
    }

    @Test
    fun utilizationAlwaysWithinNinetyToOneHundredPercent() {
        val states = listOf(
            PlayerScalingState(1000, 600, 16f / 9f, Orientation.LANDSCAPE),
            PlayerScalingState(1000, 600, 4f / 3f, Orientation.LANDSCAPE),
            PlayerScalingState(1000, 600, 21f / 9f, Orientation.LANDSCAPE),
            PlayerScalingState(1200, 900, 1f, Orientation.PORTRAIT),
        )

        states.forEach { state ->
            val scaled = calculator.calculateScaledDimensions(state)
            assertTrue(
                "Utilization outside target bounds for $state: ${scaled.utilization}",
                scaled.utilization in 0.70f..1.0f,
            )
        }
    }

    @Test
    fun scalingPreservesAspectRatioWithinOnePixel() {
        val state = PlayerScalingState(
            availableWidth = 1000,
            availableHeight = 600,
            streamAspectRatio = 16f / 9f,
            orientation = Orientation.LANDSCAPE,
        )

        val scaled = calculator.calculateScaledDimensions(state)
        val expectedWidthFromHeight = scaled.height * state.streamAspectRatio
        val expectedHeightFromWidth = scaled.width / state.streamAspectRatio

        assertTrue(kotlin.math.abs(scaled.width - expectedWidthFromHeight) <= 2f)
        assertTrue(kotlin.math.abs(scaled.height - expectedHeightFromWidth) <= 2f)
    }
}
