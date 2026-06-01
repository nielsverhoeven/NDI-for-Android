package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.database.ConnectionHistoryStateDao
import com.ndi.core.database.ConnectionHistoryStateEntity
import com.ndi.core.database.LastViewedContextDao
import com.ndi.core.database.LastViewedContextEntity
import com.ndi.core.model.ViewerVideoFrame
import com.ndi.feature.ndibrowser.data.mapper.ViewerContinuityMapper
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.File
import java.nio.file.Files

class ViewerContinuityRepositoryImplTest {

    @Test
    fun captureAndSavePreviewFrame_persistsLastViewedContext() = runTest {
        val tempDir = Files.createTempDirectory("viewer-continuity-us1").toFile()
        val repository = newRepository(tempDir)

        val savedPath = repository.captureAndSavePreviewFrame(
            sourceId = "camera-1",
            frame = sampleFrame(),
            frameCapturedAtEpochMillis = 1_000L,
        )

        val persisted = repository.getLastViewedContext()
        assertNotNull(savedPath)
        assertEquals("camera-1", persisted?.sourceId)
        assertEquals(savedPath, persisted?.lastFrameImagePath)
        assertEquals(1_000L, persisted?.lastFrameCapturedAtEpochMillis)
    }

    @Test
    fun captureAndSavePreviewFrame_replacesOlderPreviewAndKeepsSingleFile() = runTest {
        val tempDir = Files.createTempDirectory("viewer-continuity-us1-single-preview").toFile()
        val repository = newRepository(tempDir)

        val first = repository.captureAndSavePreviewFrame(
            sourceId = "camera-1",
            frame = sampleFrame(),
            frameCapturedAtEpochMillis = 10L,
        )
        val second = repository.captureAndSavePreviewFrame(
            sourceId = "camera-2",
            frame = sampleFrame(color = 0xFF00FF00.toInt()),
            frameCapturedAtEpochMillis = 20L,
        )

        assertNotNull(first)
        assertNotNull(second)
        assertFalse(File(first!!).exists())
        assertTrue(File(second!!).exists())
        assertEquals(1, tempDir.listFiles()?.count { it.isFile } ?: 0)
    }

    @Test
    fun resetPersistedStateOnAppDataClear_clearsHistoryAndLastViewedContext() = runTest {
        val tempDir = Files.createTempDirectory("viewer-continuity-us1-reset").toFile()
        val repository = newRepository(tempDir)

        repository.captureAndSavePreviewFrame(
            sourceId = "camera-reset",
            frame = sampleFrame(),
            frameCapturedAtEpochMillis = 100L,
        )
        repository.markSuccessfulFrame("camera-reset", frameCapturedAtEpochMillis = 100L)

        assertTrue(repository.hasPreviouslyConnected("camera-reset"))
        assertNotNull(repository.getLastViewedContext())

        repository.resetPersistedStateOnAppDataClear()

        assertFalse(repository.hasPreviouslyConnected("camera-reset"))
        assertNull(repository.getLastViewedContext())
        assertEquals(0, tempDir.listFiles()?.count { it.isFile } ?: 0)
    }

    private fun newRepository(directory: File): ViewerContinuityRepositoryImpl {
        return ViewerContinuityRepositoryImpl(
            lastViewedContextDao = InMemoryLastViewedContextDao(),
            connectionHistoryStateDao = InMemoryConnectionHistoryStateDao(),
            mapper = ViewerContinuityMapper(),
            previewDirectory = directory,
            previewFrameWriter = TestPreviewWriter,
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

private object TestPreviewWriter : ViewerPreviewFrameWriter {
    override fun writePng(frame: ViewerVideoFrame, outputFile: File): Boolean {
        outputFile.parentFile?.mkdirs()
        outputFile.writeText("preview-${frame.width}x${frame.height}-${frame.argbPixels.firstOrNull()}")
        return true
    }
}

private class InMemoryLastViewedContextDao : LastViewedContextDao {
    private val flow = MutableStateFlow<LastViewedContextEntity?>(null)

    override suspend fun get(contextId: String): LastViewedContextEntity? {
        return flow.value?.takeIf { it.contextId == contextId }
    }

    override fun observe(contextId: String): Flow<LastViewedContextEntity?> {
        return flow
    }

    override suspend fun upsert(entity: LastViewedContextEntity) {
        flow.value = entity
    }

    override suspend fun clear(contextId: String) {
        if (flow.value?.contextId == contextId) {
            flow.value = null
        }
    }
}

private class InMemoryConnectionHistoryStateDao : ConnectionHistoryStateDao {
    private val map = linkedMapOf<String, ConnectionHistoryStateEntity>()
    private val flow = MutableStateFlow<List<ConnectionHistoryStateEntity>>(emptyList())

    override fun observeAll(): Flow<List<ConnectionHistoryStateEntity>> {
        return flow
    }

    override suspend fun getBySourceId(sourceId: String): ConnectionHistoryStateEntity? {
        return map[sourceId]
    }

    override suspend fun upsert(entity: ConnectionHistoryStateEntity) {
        map[entity.sourceId] = entity
        flow.value = map.values.sortedByDescending { it.lastSuccessfulFrameAtEpochMillis }
    }

    override suspend fun clearAll() {
        map.clear()
        flow.value = emptyList()
    }
}
