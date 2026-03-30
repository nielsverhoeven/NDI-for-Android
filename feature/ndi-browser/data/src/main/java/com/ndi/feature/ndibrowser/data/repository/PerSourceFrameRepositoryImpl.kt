package com.ndi.feature.ndibrowser.data.repository

import android.graphics.Bitmap
import com.ndi.core.model.ViewerVideoFrame
import com.ndi.feature.ndibrowser.domain.repository.PerSourceFrameRepository
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext
import java.io.ByteArrayOutputStream
import java.io.File

/**
 * Session-scoped in-memory LRU cache for per-source thumbnail frames.
 * 
 * - Stores at most MAX_RETAINED_SOURCES (10) thumbnails concurrently
 * - Uses LinkedHashMap with access-order to track LRU
 * - Scales captured frames to ~320x height on IO dispatcher
 * - Writes thumbnails as PNG files to session cache directory
 * - Thread-safe via Mutex (all read/write ops guarded)
 * - Discards all frames on process end (no disk persistence)
 */
class PerSourceFrameRepositoryImpl(
    private val sessionCacheDir: File,
    private val bitmapWriter: ThumbnailBitmapWriter = DefaultThumbnailBitmapWriter,
) : PerSourceFrameRepository {

    private val mutex = Mutex()
    
    // LinkedHashMap with accessOrder=true for LRU behavior
    private val frameMap = LinkedHashMap<String, String>(16, 0.75f, true)

    private val _frameMapFlow = MutableStateFlow<Map<String, String>>(emptyMap())
    override fun observeFrameMap(): StateFlow<Map<String, String>> = _frameMapFlow.asStateFlow()

    init {
        // Ensure session cache directory exists
        sessionCacheDir.mkdirs()
    }

    override suspend fun saveFrameForSource(sourceId: String, frame: ViewerVideoFrame?) {
        // No-op if frame is null or sourceId is blank
        if (frame == null || sourceId.isBlank()) {
            return
        }

        mutex.withLock {
            val thumbnailPath = withContext(Dispatchers.IO) {
                writeThumbnail(frame, sourceId)
            }

            if (thumbnailPath != null) {
                // Remove old entry if exists (to update LRU order)
                frameMap.remove(sourceId)
                // Add new entry
                frameMap[sourceId] = thumbnailPath
                
                // Evict LRU entry if over cap
                if (frameMap.size > MAX_RETAINED_SOURCES) {
                    val eldestKey = frameMap.keys.first()
                    val eldestPath = frameMap.remove(eldestKey)
                    if (eldestPath != null) {
                        File(eldestPath).delete()
                    }
                }
                
                // Emit updated map
                _frameMapFlow.value = frameMap.toMap()
            }
        }
    }

    override suspend fun getFramePathForSource(sourceId: String): String? {
        return mutex.withLock {
            frameMap[sourceId]
        }
    }

    override suspend fun clearAll() {
        mutex.withLock {
            // Delete all PNG files
            frameMap.values.forEach { path ->
                File(path).delete()
            }
            frameMap.clear()
            _frameMapFlow.value = emptyMap()
        }
    }

    /**
     * Scales the frame to thumbnail resolution (~320×height, preserving aspect ratio)
     * and writes to a PNG file in the session cache directory.
     *
     * Returns the absolute path to the PNG file, or null if write fails.
     */
    private suspend fun writeThumbnail(frame: ViewerVideoFrame, sourceId: String): String? {
        return withContext(Dispatchers.IO) {
            try {
                // Write to file via the bitmap writer
                val sanitizedSourceId = sanitizeForFilename(sourceId)
                val outputFile = File(sessionCacheDir, "$sanitizedSourceId.png")
                outputFile.parentFile?.mkdirs()

                val success = bitmapWriter.writeThumbnailFrame(frame, outputFile)

                if (success) outputFile.absolutePath else null
            } catch (e: Exception) {
                null
            }
        }
    }

    /**
     * Sanitizes a source ID to be a valid filename (removes special chars, etc).
     */
    private fun sanitizeForFilename(sourceId: String): String {
        return sourceId
            .replace(Regex("[^a-zA-Z0-9._-]"), "_")
            .take(200) // Limit filename length
    }

    companion object {
        private const val MAX_RETAINED_SOURCES = PerSourceFrameRepository.MAX_RETAINED_SOURCES
    }
}

/**
 * Strategy interface for writing thumbnail frames to PNG files (for testing).
 */
interface ThumbnailBitmapWriter {
    fun writeThumbnailFrame(frame: ViewerVideoFrame, outputFile: File): Boolean
}

/**
 * Default implementation converts frame to bitmap, scales it, and writes as PNG.
 */
object DefaultThumbnailBitmapWriter : ThumbnailBitmapWriter {
    override fun writeThumbnailFrame(frame: ViewerVideoFrame, outputFile: File): Boolean {
        return try {
            // Convert ARGB pixel array to Bitmap
            val bitmap = Bitmap.createBitmap(
                frame.argbPixels,
                frame.width,
                frame.height,
                Bitmap.Config.ARGB_8888,
            )

            // Scale to ~320×height preserving aspect ratio
            val scaledBitmap = scaleBitmap(bitmap, targetWidth = 320)
            bitmap.recycle()

            // Write scaled bitmap as PNG
            ByteArrayOutputStream().use { outputStream ->
                scaledBitmap.compress(Bitmap.CompressFormat.PNG, 100, outputStream)
                val bytes = outputStream.toByteArray()
                outputFile.writeBytes(bytes)
                scaledBitmap.recycle()
                true
            }
        } catch (e: Exception) {
            false
        }
    }

    private fun scaleBitmap(bitmap: Bitmap, targetWidth: Int): Bitmap {
        val aspectRatio = bitmap.height.toFloat() / bitmap.width.toFloat()
        val targetHeight = (targetWidth * aspectRatio).toInt()
        return Bitmap.createScaledBitmap(bitmap, targetWidth, targetHeight, true)
    }
}
