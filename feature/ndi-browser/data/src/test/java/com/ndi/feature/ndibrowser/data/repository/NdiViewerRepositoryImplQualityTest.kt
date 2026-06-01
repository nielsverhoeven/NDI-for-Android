package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.database.ViewerSessionDao
import com.ndi.core.database.ViewerSessionEntity
import com.ndi.core.model.ViewerVideoFrame
import com.ndi.feature.ndibrowser.domain.repository.QualityProfile
import com.ndi.feature.ndibrowser.domain.repository.QualityProfileApplyResult
import com.ndi.sdkbridge.NdiViewerBridge
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class NdiViewerRepositoryImplQualityTest {

    @Test
    fun applyingSmoothProfile_setsFrameRateCapTo30() = runTest {
        val bridge = LocalFakeViewerBridge(measuredFps = 30f, actualWidth = 854, actualHeight = 480)
        val repository = NdiViewerRepositoryImpl(bridge = bridge, viewerSessionDao = LocalFakeViewerSessionDao())
        repository.connectToSource("camera-1")

        val result = repository.applyQualityProfile(QualityProfile.Smooth)

        assertEquals(QualityProfileApplyResult.APPLIED, result)
    }

    @Test
    fun applyingBalancedProfile_setsResolutionTo1080p() = runTest {
        val bridge = LocalFakeViewerBridge(measuredFps = 30f, actualWidth = 1280, actualHeight = 720)
        val repository = NdiViewerRepositoryImpl(bridge = bridge, viewerSessionDao = LocalFakeViewerSessionDao())
        repository.connectToSource("camera-1")

        repository.applyQualityProfile(QualityProfile.Balanced)
        val stats = repository.getOptimizationStats().first { it.sourceId == "camera-1" && it.actualWidth > 0 }

        assertEquals(1280, stats.actualWidth)
        assertEquals(720, stats.actualHeight)
    }

    @Test
    fun applyingHighQualityProfile_returnsFallbackWhenOnlyOnePolicyApplies() = runTest {
        val bridge = LocalFakeViewerBridge(
            measuredFps = 30f,
            actualWidth = 1920,
            actualHeight = 1080,
            frameRatePolicySupported = true,
            resolutionPolicySupported = false,
        )
        val repository = NdiViewerRepositoryImpl(bridge = bridge, viewerSessionDao = LocalFakeViewerSessionDao())
        repository.connectToSource("camera-1")

        val result = repository.applyQualityProfile(QualityProfile.HighQuality)

        assertEquals(QualityProfileApplyResult.FALLBACK, result)
    }

    @Test
    fun getOptimizationStats_returnsCurrentMetricsWithinOneSecond() = runTest {
        val bridge = LocalFakeViewerBridge(measuredFps = 24f, actualWidth = 1920, actualHeight = 1080)
        val repository = NdiViewerRepositoryImpl(bridge = bridge, viewerSessionDao = LocalFakeViewerSessionDao())
        repository.connectToSource("camera-1")

        repository.applyQualityProfile(QualityProfile.Smooth)
        val stats = repository.getOptimizationStats().first { it.sourceId == "camera-1" && it.currentFrameRate > 0.0 }

        assertTrue(stats.currentFrameRate in 24.0..60.0)
        assertTrue(stats.actualWidth > 0 && stats.actualHeight > 0)
    }

    @Test
    fun profileApplication_doesNotCrashOnCodecUnsupportedError() = runTest {
        val bridge = LocalFakeViewerBridge(
            measuredFps = 30f,
            actualWidth = 1920,
            actualHeight = 1080,
            frameRatePolicySupported = false,
            resolutionPolicySupported = false,
        )
        val repository = NdiViewerRepositoryImpl(bridge = bridge, viewerSessionDao = LocalFakeViewerSessionDao())
        repository.connectToSource("camera-1")

        val result = repository.applyQualityProfile(QualityProfile.HighQuality)

        assertEquals(QualityProfileApplyResult.NOT_SUPPORTED, result)
    }
}

private class LocalFakeViewerSessionDao : ViewerSessionDao {
    private var latest: ViewerSessionEntity? = null

    override suspend fun getLatest(): ViewerSessionEntity? = latest

    override suspend fun upsert(session: ViewerSessionEntity) {
        latest = session
    }
}

private class LocalFakeViewerBridge(
    private val measuredFps: Float,
    private val actualWidth: Int,
    private val actualHeight: Int,
    private val frameRatePolicySupported: Boolean = true,
    private val resolutionPolicySupported: Boolean = true,
    private val dropPercent: Float = 0f,
) : NdiViewerBridge {
    private var running = false

    override fun startReceiver(sourceId: String) {
        running = true
    }

    override fun stopReceiver() {
        running = false
    }

    override fun getLatestReceiverFrame(): ViewerVideoFrame? {
        if (!running) return null
        return ViewerVideoFrame(width = actualWidth, height = actualHeight, argbPixels = IntArray(actualWidth * actualHeight))
    }

    override fun applyReceiverQualityProfile(profileId: String, maxWidth: Int, maxHeight: Int, targetFps: Int) = Unit

    override fun setFrameRatePolicy(targetFps: Int): Boolean = frameRatePolicySupported

    override fun setResolutionPolicy(width: Int, height: Int): Boolean = resolutionPolicySupported

    override fun getReceiverDroppedFramePercent(): Float = dropPercent

    override fun getActualResolution(): Pair<Int, Int> = actualWidth to actualHeight

    override fun getMeasuredReceiverFps(): Float = measuredFps
}
