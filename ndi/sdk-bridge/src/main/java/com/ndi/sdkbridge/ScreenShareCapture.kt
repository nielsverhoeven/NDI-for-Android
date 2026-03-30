package com.ndi.sdkbridge

import android.app.Activity
import android.content.Context
import android.content.Intent
import android.graphics.PixelFormat
import android.hardware.display.DisplayManager
import android.hardware.display.VirtualDisplay
import android.media.Image
import android.media.ImageReader
import android.media.projection.MediaProjection
import android.media.projection.MediaProjectionManager
import android.os.Handler
import android.os.HandlerThread
import android.util.Log
import java.util.UUID
import java.util.concurrent.ConcurrentHashMap

internal data class ScreenCapturePermissionGrant(
    val resultCode: Int,
    val data: Intent,
)

internal object ScreenCapturePermissionStore {
    private val grants = ConcurrentHashMap<String, ScreenCapturePermissionGrant>()

    fun register(resultCode: Int, data: Intent?): String? {
        if (resultCode != Activity.RESULT_OK || data == null) return null
        val token = UUID.randomUUID().toString()
        grants[token] = ScreenCapturePermissionGrant(
            resultCode = resultCode,
            data = Intent(data),
        )
        return token
    }

    fun get(token: String?): ScreenCapturePermissionGrant? {
        if (token.isNullOrBlank()) return null
        return grants[token]
    }
}

internal object ScreenShareController {
    private const val TAG = "NdiScreenShare"

    private val lock = Any()
    private var handlerThread: HandlerThread? = null
    private var imageReader: ImageReader? = null
    private var virtualDisplay: VirtualDisplay? = null
    private var mediaProjection: MediaProjection? = null
    private var mediaProjectionCallback: MediaProjection.Callback? = null
    private var appContext: Context? = null

    fun start(
        context: Context,
        grant: ScreenCapturePermissionGrant,
        streamName: String,
        onFrame: (width: Int, height: Int, argbPixels: IntArray) -> Unit,
    ) {
        synchronized(lock) {
            stopLocked()
            val applicationContext = context.applicationContext
            appContext = applicationContext
            var startupPhase = "startForegroundService"
            ScreenShareForegroundService.start(applicationContext, streamName)

            val projectionManager = context.getSystemService(Context.MEDIA_PROJECTION_SERVICE) as? MediaProjectionManager
                ?: error("MediaProjectionManager is unavailable")
            try {
                startupPhase = "getMediaProjection"
                val projection = projectionManager.getMediaProjection(grant.resultCode, Intent(grant.data))
                    ?: error("Unable to obtain MediaProjection")

                val metrics = context.resources.displayMetrics
                val width = metrics.widthPixels.coerceAtLeast(1)
                val height = metrics.heightPixels.coerceAtLeast(1)
                val densityDpi = metrics.densityDpi.coerceAtLeast(1)

                startupPhase = "startCaptureThread"
                val captureThread = HandlerThread("ndi-screen-share").apply { start() }
                val handler = Handler(captureThread.looper)

                startupPhase = "createImageReader"
                val reader = ImageReader.newInstance(width, height, PixelFormat.RGBA_8888, 2)
                val projectionCallback = object : MediaProjection.Callback() {
                    override fun onStop() {
                        Log.i(TAG, "MediaProjection stopped by the system")
                        stop()
                    }
                }

                startupPhase = "registerCallback"
                projection.registerCallback(projectionCallback, handler)

                startupPhase = "setImageListener"
                reader.setOnImageAvailableListener({ source ->
                    val image = source.acquireLatestImage() ?: return@setOnImageAvailableListener
                    try {
                        val pixels = image.toArgbPixels() ?: return@setOnImageAvailableListener
                        onFrame(width, height, pixels)
                    } catch (error: Throwable) {
                        Log.e(TAG, "Failed to process screen capture frame", error)
                    } finally {
                        image.close()
                    }
                }, handler)

                startupPhase = "createVirtualDisplay"
                val display = projection.createVirtualDisplay(
                    "ndi-screen-share",
                    width,
                    height,
                    densityDpi,
                    DisplayManager.VIRTUAL_DISPLAY_FLAG_AUTO_MIRROR,
                    reader.surface,
                    null,
                    handler,
                ) ?: error("Unable to create virtual display")

                mediaProjection = projection
                mediaProjectionCallback = projectionCallback
                virtualDisplay = display
                imageReader = reader
                handlerThread = captureThread
                Log.i(TAG, "Screen share capture started successfully")
            } catch (error: Throwable) {
                Log.e(TAG, "Failed during screen share startup phase: $startupPhase", error)
                stopLocked()
                throw error
            }
        }
    }

    fun stop() {
        synchronized(lock) {
            stopLocked()
        }
    }

    private fun stopLocked() {
        runCatching { virtualDisplay?.release() }
        virtualDisplay = null

        runCatching { imageReader?.setOnImageAvailableListener(null, null) }
        runCatching { imageReader?.close() }
        imageReader = null

        runCatching {
            val projection = mediaProjection
            val callback = mediaProjectionCallback
            if (projection != null && callback != null) {
                projection.unregisterCallback(callback)
            }
        }
        mediaProjectionCallback = null

        runCatching { mediaProjection?.stop() }
        mediaProjection = null

        appContext?.let { context ->
            runCatching { ScreenShareForegroundService.stop(context) }
        }
        appContext = null

        handlerThread?.quitSafely()
        handlerThread = null
    }

    private fun Image.toArgbPixels(): IntArray? {
        if (planes.isEmpty()) return null

        val plane = planes[0]
        val buffer = plane.buffer.duplicate()
        val pixelStride = plane.pixelStride
        val rowStride = plane.rowStride
        val result = IntArray(width * height)

        for (y in 0 until height) {
            val rowBase = y * rowStride
            for (x in 0 until width) {
                val index = rowBase + (x * pixelStride)
                val red = buffer.get(index).toInt() and 0xFF
                val green = buffer.get(index + 1).toInt() and 0xFF
                val blue = buffer.get(index + 2).toInt() and 0xFF
                val alpha = buffer.get(index + 3).toInt() and 0xFF
                result[(y * width) + x] =
                    (alpha shl 24) or
                    (red shl 16) or
                    (green shl 8) or
                    blue
            }
        }

        return result
    }
}