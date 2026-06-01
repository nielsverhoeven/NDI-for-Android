package com.ndi.feature.ndibrowser.viewer

import com.ndi.feature.ndibrowser.testutil.MainDispatcherRule
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertSame
import org.junit.Rule
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class PlayerScalingViewModelTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    @Test
    fun portraitBounds_usesExpectedScaledResult() {
        val viewModel = PlayerScalingViewModel(PlayerScalingCalculatorImpl())

        viewModel.updatePlayerBounds(
            width = 1000,
            height = 800,
            streamAspectRatio = 16f / 9f,
            orientation = Orientation.PORTRAIT,
        )
        val scaled = viewModel.scaledDimensions.value

        assertEquals(1000, scaled?.width)
        assertEquals(562, scaled?.height)
    }

    @Test
    fun landscapeBounds_recalculatesExpectedResultInSameViewModel() {
        val viewModel = PlayerScalingViewModel(PlayerScalingCalculatorImpl())

        viewModel.updatePlayerBounds(
            width = 1000,
            height = 800,
            streamAspectRatio = 16f / 9f,
            orientation = Orientation.PORTRAIT,
        )
        viewModel.updatePlayerBounds(
            width = 1000,
            height = 600,
            streamAspectRatio = 16f / 9f,
            orientation = Orientation.LANDSCAPE,
        )

        val scaled = viewModel.scaledDimensions.value
        assertEquals(1000, scaled?.width)
        assertEquals(562, scaled?.height)
    }

    @Test
    fun emitsNewScalingStateOnConfigurationChange() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val viewModel = PlayerScalingViewModel(PlayerScalingCalculatorImpl())

        viewModel.updatePlayerBounds(
            width = 1000,
            height = 800,
            streamAspectRatio = 16f / 9f,
            orientation = Orientation.PORTRAIT,
        )
        val portrait = viewModel.scaledDimensions.value
        viewModel.updatePlayerBounds(
            width = 1000,
            height = 600,
            streamAspectRatio = 16f / 9f,
            orientation = Orientation.LANDSCAPE,
        )
        advanceUntilIdle()
        val landscape = viewModel.scaledDimensions.value

        assertNotEquals(portrait, landscape)
        assertEquals(1000, portrait?.width)
        assertEquals(1000, landscape?.width)
    }

    @Test
    fun duplicateBoundsAspect_doNotEmitRedundantStates() {
        val viewModel = PlayerScalingViewModel(PlayerScalingCalculatorImpl())
        viewModel.updatePlayerBounds(1000, 600, 16f / 9f, Orientation.LANDSCAPE)
        val first = viewModel.scaledDimensions.value
        viewModel.updatePlayerBounds(1000, 600, 16f / 9f, Orientation.LANDSCAPE)
        val second = viewModel.scaledDimensions.value

        assertSame(first, second)
    }
}
