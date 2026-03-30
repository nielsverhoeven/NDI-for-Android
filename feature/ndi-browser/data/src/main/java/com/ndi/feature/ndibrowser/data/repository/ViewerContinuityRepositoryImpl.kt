package com.ndi.feature.ndibrowser.data.repository

import android.graphics.Bitmap
import com.ndi.core.database.ConnectionHistoryStateDao
import com.ndi.core.database.LastViewedContextDao
import com.ndi.core.model.ViewerVideoFrame
import com.ndi.feature.ndibrowser.data.mapper.ViewerContinuityMapper
import com.ndi.feature.ndibrowser.domain.repository.ConnectionHistoryState
import com.ndi.feature.ndibrowser.domain.repository.LastViewedContext
import com.ndi.feature.ndibrowser.domain.repository.ViewerContinuityRepository
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.withContext
import java.io.File

fun interface ViewerPreviewFrameWriter {
    fun writePng(frame: ViewerVideoFrame, outputFile: File): Boolean
}

private object AndroidBitmapPreviewFrameWriter : ViewerPreviewFrameWriter {
    override fun writePng(frame: ViewerVideoFrame, outputFile: File): Boolean {
        if (frame.width <= 0 || frame.height <= 0 || frame.argbPixels.isEmpty()) {
            return false
        }
        return runCatching {
            outputFile.parentFile?.mkdirs()
            val bitmap = Bitmap.createBitmap(
                frame.argbPixels,
                frame.width,
                frame.height,
                Bitmap.Config.ARGB_8888,
            )
            outputFile.outputStream().use { stream ->
                bitmap.compress(Bitmap.CompressFormat.PNG, 100, stream)
            }
            bitmap.recycle()
            true
        }.getOrDefault(false)
    }
}

class ViewerContinuityRepositoryImpl(
    private val lastViewedContextDao: LastViewedContextDao,
    private val connectionHistoryStateDao: ConnectionHistoryStateDao,
    private val mapper: ViewerContinuityMapper = ViewerContinuityMapper(),
    private val previewDirectory: File = File(System.getProperty("java.io.tmpdir"), "ndi-viewer-previews"),
    private val previewFrameWriter: ViewerPreviewFrameWriter = AndroidBitmapPreviewFrameWriter,
) : ViewerContinuityRepository {

    override fun observeLastViewedContext(): Flow<LastViewedContext?> {
        return lastViewedContextDao.observe().map { entity ->
            entity?.let(mapper::toLastViewedContext)
        }
    }

    override suspend fun getLastViewedContext(): LastViewedContext? {
        return withContext(Dispatchers.IO) {
            lastViewedContextDao.get()?.let(mapper::toLastViewedContext)
        }
    }

    override suspend fun saveLastViewedContext(context: LastViewedContext) {
        if (context.sourceId.isBlank()) return
        withContext(Dispatchers.IO) {
            lastViewedContextDao.upsert(mapper.toLastViewedContextEntity(context))
        }
    }

    override suspend fun clearLastViewedContext() {
        withContext(Dispatchers.IO) {
            lastViewedContextDao.clear()
        }
    }

    override fun observeConnectionHistory(): Flow<List<ConnectionHistoryState>> {
        return connectionHistoryStateDao.observeAll().map { entities ->
            entities.map(mapper::toConnectionHistoryState)
        }
    }

    override fun observePreviouslyConnectedSourceIds(): Flow<Set<String>> {
        return observeConnectionHistory().map { states ->
            states.asSequence()
                .filter { it.previouslyConnected }
                .map { it.sourceId }
                .toSet()
        }.distinctUntilChanged()
    }

    override suspend fun markSuccessfulFrame(sourceId: String, frameCapturedAtEpochMillis: Long) {
        if (sourceId.isBlank()) return

        withContext(Dispatchers.IO) {
            val existing = connectionHistoryStateDao.getBySourceId(sourceId)
            val updated = if (existing == null) {
                ConnectionHistoryState(
                    sourceId = sourceId,
                    previouslyConnected = true,
                    firstSuccessfulFrameAtEpochMillis = frameCapturedAtEpochMillis,
                    lastSuccessfulFrameAtEpochMillis = frameCapturedAtEpochMillis,
                )
            } else {
                mapper.toConnectionHistoryState(existing).copy(
                    previouslyConnected = true,
                    lastSuccessfulFrameAtEpochMillis = maxOf(
                        existing.lastSuccessfulFrameAtEpochMillis,
                        frameCapturedAtEpochMillis,
                    ),
                )
            }
            connectionHistoryStateDao.upsert(mapper.toConnectionHistoryEntity(updated))
        }
    }

    override suspend fun captureAndSavePreviewFrame(
        sourceId: String,
        frame: ViewerVideoFrame,
        frameCapturedAtEpochMillis: Long,
    ): String? {
        if (sourceId.isBlank()) return null

        return withContext(Dispatchers.IO) {
            val previous = lastViewedContextDao.get()
            val outputFile = File(
                previewDirectory,
                "preview_${sanitizeSourceId(sourceId)}_${frameCapturedAtEpochMillis}.png",
            )
            val writeSucceeded = previewFrameWriter.writePng(frame, outputFile)
            if (!writeSucceeded) {
                saveLastViewedContext(
                    LastViewedContext(
                        sourceId = sourceId,
                        lastFrameImagePath = previous?.lastFrameImagePath,
                        lastFrameCapturedAtEpochMillis = frameCapturedAtEpochMillis,
                    ),
                )
                return@withContext previous?.lastFrameImagePath
            }

            removeStalePreviewFiles(keep = outputFile, previousPath = previous?.lastFrameImagePath)
            val path = outputFile.absolutePath
            saveLastViewedContext(
                LastViewedContext(
                    sourceId = sourceId,
                    lastFrameImagePath = path,
                    lastFrameCapturedAtEpochMillis = frameCapturedAtEpochMillis,
                ),
            )
            path
        }
    }

    override suspend fun resetPersistedStateOnAppDataClear() {
        withContext(Dispatchers.IO) {
            val previous = lastViewedContextDao.get()
            clearLastViewedContext()
            clearConnectionHistory()
            previous?.lastFrameImagePath?.let { path ->
                runCatching { File(path).delete() }
            }
            previewDirectory.listFiles()?.forEach { file ->
                if (file.isFile) {
                    runCatching { file.delete() }
                }
            }
        }
    }

    override suspend fun getConnectionHistory(sourceId: String): ConnectionHistoryState? {
        if (sourceId.isBlank()) return null
        return withContext(Dispatchers.IO) {
            connectionHistoryStateDao.getBySourceId(sourceId)?.let(mapper::toConnectionHistoryState)
        }
    }

    override suspend fun hasPreviouslyConnected(sourceId: String): Boolean {
        return getConnectionHistory(sourceId)?.previouslyConnected == true
    }

    override suspend fun clearConnectionHistory() {
        withContext(Dispatchers.IO) {
            connectionHistoryStateDao.clearAll()
        }
    }

    private fun sanitizeSourceId(sourceId: String): String {
        return sourceId.replace(Regex("[^A-Za-z0-9._-]"), "_")
    }

    private fun removeStalePreviewFiles(keep: File, previousPath: String?) {
        if (!previewDirectory.exists()) return
        previewDirectory.listFiles()?.forEach { candidate ->
            if (!candidate.isFile) return@forEach
            if (candidate.absolutePath != keep.absolutePath) {
                runCatching { candidate.delete() }
            }
        }
        if (!previousPath.isNullOrBlank() && previousPath != keep.absolutePath) {
            runCatching { File(previousPath).delete() }
        }
    }
}
