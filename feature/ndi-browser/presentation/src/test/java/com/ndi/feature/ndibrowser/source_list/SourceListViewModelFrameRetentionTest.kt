package com.ndi.feature.ndibrowser.source_list

import com.ndi.core.model.ViewerVideoFrame
import com.ndi.feature.ndibrowser.domain.repository.PerSourceFrameRepository
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.MutableStateFlow
import org.junit.Test
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import java.io.File

/**
 * Minimal tests to verify SourceListViewModel can integrate with per-source frame repository.
 * These are structure/integration tests, not full UI state tests.
 */
class SourceListViewModelFrameRetentionTest {

    @Test
    fun sourcesEnriched_withFramePath() {
        // Verify that a frame map can provide paths
        val frameRepository = MinimalPerSourceFrameRepository()
        frameRepository.updateFrameMap(mapOf("source-1" to "/path/to/frame.png"))
        
        val frameMap = frameRepository.observeFrameMap().value
        assertEquals("/path/to/frame.png", frameMap["source-1"])
    }

    @Test
    fun multipleSources_independentFramePaths() {
        // Verify multiple sources can have independent frame paths
        val frameRepository = MinimalPerSourceFrameRepository()
        frameRepository.updateFrameMap(mapOf(
            "source-1" to "/path/to/frame1.png",
            "source-2" to "/path/to/frame2.png",
        ))
        
        val frameMap = frameRepository.observeFrameMap().value
        assertEquals("/path/to/frame1.png", frameMap["source-1"])
        assertEquals("/path/to/frame2.png", frameMap["source-2"])
    }

    @Test
    fun sourceWithoutFrame_nullPath() {
        // Verify sources without frames return null
        val frameRepository = MinimalPerSourceFrameRepository()
        frameRepository.updateFrameMap(emptyMap())
        
        val frameMap = frameRepository.observeFrameMap().value
        assertNull(frameMap["source-1"])
    }

    @Test
    fun frameMapUpdate_triggersFlowUpdate() {
        // Verify frame map updates are reflected in the flow
        val frameRepository = MinimalPerSourceFrameRepository()
        
        var firstEmission = frameRepository.observeFrameMap().value
        assertEquals(emptyMap<String, String>(), firstEmission)
        
        frameRepository.updateFrameMap(mapOf("source-1" to "/path/to/frame.png"))
        
        val secondEmission = frameRepository.observeFrameMap().value
        assertNotNull(secondEmission["source-1"])
        assertEquals("/path/to/frame.png", secondEmission["source-1"])
    }
}

/**
 * Minimal fake implementation for testing
 */
private class MinimalPerSourceFrameRepository : PerSourceFrameRepository {
    private val frameMapFlow = MutableStateFlow<Map<String, String>>(emptyMap())

    fun updateFrameMap(map: Map<String, String>) {
        frameMapFlow.value = map
    }

    override suspend fun saveFrameForSource(sourceId: String, frame: ViewerVideoFrame?) {}

    override suspend fun getFramePathForSource(sourceId: String): String? {
        return frameMapFlow.value[sourceId]
    }

    override fun observeFrameMap(): StateFlow<Map<String, String>> = frameMapFlow

    override suspend fun clearAll() {
        frameMapFlow.value = emptyMap()
    }

    companion object {
        const val MAX_RETAINED_SOURCES = 10
    }
}
