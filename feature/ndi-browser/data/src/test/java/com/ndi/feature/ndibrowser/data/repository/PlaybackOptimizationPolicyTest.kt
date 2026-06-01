package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.database.ViewerSessionDao
import com.ndi.core.database.ViewerSessionEntity
import com.ndi.core.model.PlaybackState
import com.ndi.core.model.ViewerSession
import com.ndi.feature.ndibrowser.domain.repository.QualityProfile
import com.ndi.sdkbridge.NdiViewerBridge
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.UUID

class PlaybackOptimizationPolicyTest {

    @Test
    fun degradeQualityIfNeeded_movesToNextLowerProfileWhenThresholdBreached() = runTest {
        val bridge = FakeViewerBridge(measuredFps = 18f, dropPercent = 18f)
        val repository = NdiViewerRepositoryImpl(bridge = bridge, viewerSessionDao = FakeViewerSessionDao())
        repository.connectToSource("camera-1")
        repository.applyQualityProfile("camera-1", QualityProfile.HighQuality)

        val degraded = repository.degradeQualityIfNeeded("camera-1", droppedFramePercent = 18)

        assertEquals(QualityProfile.Balanced, degraded)
        assertEquals(QualityProfile.Balanced, repository.getActiveQualityProfile("camera-1"))
    }

    @Test
    fun degradeQualityIfNeeded_holdsProfileWhenThresholdNotBreached() = runTest {
        val bridge = FakeViewerBridge(measuredFps = 29f, dropPercent = 4f)
        val repository = NdiViewerRepositoryImpl(bridge = bridge, viewerSessionDao = FakeViewerSessionDao())
        repository.connectToSource("camera-1")
        repository.applyQualityProfile("camera-1", QualityProfile.Balanced)

        val profile = repository.degradeQualityIfNeeded("camera-1", droppedFramePercent = 4)

        assertEquals(QualityProfile.Balanced, profile)
    }

    @Test
    fun optimizationStats_reflectsBridgeMetricsForActivePlayback() = runTest {
        val bridge = FakeViewerBridge(measuredFps = 27f, dropPercent = 6f, actualWidth = 1280, actualHeight = 720)
        val repository = NdiViewerRepositoryImpl(bridge = bridge, viewerSessionDao = FakeViewerSessionDao())
        repository.connectToSource("camera-1")
        repository.applyQualityProfile("camera-1", QualityProfile.Balanced)

        val sample = repository.getOptimizationStats().first { it.sourceId == "camera-1" && it.currentFrameRate > 0.0 }

        assertEquals(27.0, sample.currentFrameRate, 0.01)
        assertEquals(1280, sample.actualWidth)
        assertEquals(720, sample.actualHeight)
        assertTrue(sample.droppedFramePercent >= 0)
    }
}

private class FakeViewerSessionDao : ViewerSessionDao {
    private var latest: ViewerSessionEntity? = null

    override suspend fun getLatest(): ViewerSessionEntity? = latest

    override suspend fun upsert(session: ViewerSessionEntity) {
        latest = session
    }
}

private class FakeViewerBridge(
    private val measuredFps: Float = 30f,
    private val dropPercent: Float = 0f,
    private val actualWidth: Int = 1920,
    private val actualHeight: Int = 1080,
) : NdiViewerBridge {
    private var running = false

    override fun startReceiver(sourceId: String) {
        running = true
    }

    override fun stopReceiver() {
        running = false
    }

    override fun getLatestReceiverFrame() = if (running) {
        com.ndi.core.model.ViewerVideoFrame(width = actualWidth, height = actualHeight, argbPixels = IntArray(actualWidth * actualHeight))
    } else {
        null
    }

    override fun applyReceiverQualityProfile(profileId: String, maxWidth: Int, maxHeight: Int, targetFps: Int) = Unit

    override fun setFrameRatePolicy(targetFps: Int): Boolean = true

    override fun setResolutionPolicy(width: Int, height: Int): Boolean = true

    override fun getReceiverDroppedFramePercent(): Float = dropPercent

    override fun getActualResolution(): Pair<Int, Int> = actualWidth to actualHeight

    override fun getMeasuredReceiverFps(): Float = measuredFps
}
