package com.ndi.feature.ndibrowser.data.repository

import android.graphics.Bitmap
import com.ndi.core.model.ViewerVideoFrame
import com.ndi.feature.ndibrowser.domain.repository.PerSourceFrameRepository
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import java.io.File
import java.nio.file.Files

class PerSourceFrameRepositoryImplTest {

    private lateinit var tempDir: File
    private lateinit var repository: PerSourceFrameRepositoryImpl

    @Before
    fun setUp() {
        tempDir = Files.createTempDirectory("per-source-frame-repo-test").toFile()
    }

    @Test
    fun saveFrameForSource_validFrame_storesEntry() = runTest {
        repository = createRepository()
        
        repository.saveFrameForSource("source-1", sampleFrame())
        
        val frameMap = repository.observeFrameMap().first()
        assertTrue(frameMap.containsKey("source-1"))
        assertNotNull(frameMap["source-1"])
    }

    @Test
    fun saveFrameForSource_nullFrame_noOp() = runTest {
        repository = createRepository()
        
        repository.saveFrameForSource("source-1", null)
        
        val frameMap = repository.observeFrameMap().first()
        assertTrue(frameMap.isEmpty())
    }

    @Test
    fun saveFrameForSource_blankSourceId_noOp() = runTest {
        repository = createRepository()
        
        repository.saveFrameForSource("", sampleFrame())
        
        val frameMap = repository.observeFrameMap().first()
        assertTrue(frameMap.isEmpty())
    }

    @Test
    fun saveFrameForSource_sameSource_updateExisting() = runTest {
        repository = createRepository()
        
        val frame1 = sampleFrame(color = 0xFFFF0000.toInt())
        repository.saveFrameForSource("source-1", frame1)
        var frameMap = repository.observeFrameMap().first()
        val firstPath = frameMap["source-1"]
        
        val frame2 = sampleFrame(color = 0xFF00FF00.toInt())
        repository.saveFrameForSource("source-1", frame2)
        frameMap = repository.observeFrameMap().first()
        val secondPath = frameMap["source-1"]
        
        // Should have same key but only one entry
        assertEquals(1, frameMap.size)
        // Path may be the same file updated internally
        assertNotNull(secondPath)
        assertTrue(File(secondPath!!).exists())
    }

    @Test
    fun saveFrameForSource_11Frames_evictsLRU() = runTest {
        repository = createRepository()
        
        // Save 11 frames (one more than max of 10)
        repeat(11) { i ->
            repository.saveFrameForSource("source-$i", sampleFrame(color = 0xFFFF0000.toInt() + i))
        }
        
        val frameMap = repository.observeFrameMap().first()
        
        // Should only have 10 entries (LRU evicted the first one)
        assertEquals(10, frameMap.size)
        assertNull(frameMap["source-0"])
        assertTrue(frameMap.containsKey("source-10"))
    }

    @Test
    fun getFramePathForSource_unseenSource_returnsNull() = runTest {
        repository = createRepository()
        
        val path = repository.getFramePathForSource("source-not-seen")
        
        assertNull(path)
    }

    @Test
    fun getFramePathForSource_seenSource_returnsPath() = runTest {
        repository = createRepository()
        
        repository.saveFrameForSource("source-1", sampleFrame())
        val path = repository.getFramePathForSource("source-1")
        
        assertNotNull(path)
        assertTrue(File(path!!).exists())
    }

    @Test
    fun observeFrameMap_initialEmission_emitsEmptyMap() = runTest {
        repository = createRepository()
        
        val frameMap = repository.observeFrameMap().first()
        
        assertEquals(emptyMap<String, String>(), frameMap)
    }

    @Test
    fun observeFrameMap_afterSave_emitsUpdatedMap() = runTest {
        repository = createRepository()
        
        val initialMap = repository.observeFrameMap().first()
        assertEquals(emptyMap<String, String>(), initialMap)
        
        repository.saveFrameForSource("source-1", sampleFrame())
        val updatedMap = repository.observeFrameMap().first()
        
        assertTrue(updatedMap.containsKey("source-1"))
    }

    @Test
    fun clearAll_removesEntriesAndEmitsEmpty() = runTest {
        repository = createRepository()
        
        repository.saveFrameForSource("source-1", sampleFrame())
        repository.saveFrameForSource("source-2", sampleFrame())
        var frameMap = repository.observeFrameMap().first()
        assertEquals(2, frameMap.size)
        
        repository.clearAll()
        frameMap = repository.observeFrameMap().first()
        
        assertEquals(emptyMap<String, String>(), frameMap)
    }

    private fun createRepository(): PerSourceFrameRepositoryImpl {
        return PerSourceFrameRepositoryImpl(
            sessionCacheDir = tempDir,
            bitmapWriter = TestThumbnailWriter,
        )
    }

    private fun sampleFrame(color: Int = 0xFFFF0000.toInt()): ViewerVideoFrame {
        return ViewerVideoFrame(
            width = 2,
            height = 2,
            argbPixels = intArrayOf(color, color, color, color),
        )
    }
}

/**
 * Test implementation that writes a dummy file instead of actual PNG.
 */
private object TestThumbnailWriter : ThumbnailBitmapWriter {
    override fun writeThumbnailFrame(frame: ViewerVideoFrame, outputFile: File): Boolean {
        return try {
            outputFile.parentFile?.mkdirs()
            outputFile.writeText("test-thumbnail-${frame.width}x${frame.height}")
            true
        } catch (e: Exception) {
            false
        }
    }
}
