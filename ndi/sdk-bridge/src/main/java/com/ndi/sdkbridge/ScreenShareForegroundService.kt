package com.ndi.sdkbridge

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.Service
import android.content.Context
import android.content.Intent
import android.os.Build
import android.os.IBinder
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit

internal class ScreenShareForegroundService : Service() {

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_STOP -> {
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopSelf()
                return START_NOT_STICKY
            }

            else -> {
                val streamName = intent?.getStringExtra(EXTRA_STREAM_NAME).orEmpty().ifBlank {
                    getString(R.string.screen_share_notification_title)
                }
                startForegroundInternal(streamName)
                signalStarted()
                return START_STICKY
            }
        }
    }

    private fun startForegroundInternal(streamName: String) {
        val manager = getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val channel = NotificationChannel(
                CHANNEL_ID,
                getString(R.string.screen_share_notification_channel_name),
                NotificationManager.IMPORTANCE_LOW,
            ).apply {
                description = getString(R.string.screen_share_notification_channel_description)
                setShowBadge(false)
            }
            manager.createNotificationChannel(channel)
        }

        val notificationBuilder = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            Notification.Builder(this, CHANNEL_ID)
        } else {
            Notification.Builder(this)
        }

        val notification = notificationBuilder
            .setContentTitle(getString(R.string.screen_share_notification_title))
            .setContentText(getString(R.string.screen_share_notification_text, streamName))
            .setSmallIcon(android.R.drawable.stat_sys_warning)
            .setOngoing(true)
            .build()

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            startForeground(
                NOTIFICATION_ID,
                notification,
                ServiceInfoCompat.FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION,
            )
        } else {
            startForeground(NOTIFICATION_ID, notification)
        }
    }

    companion object {
        private const val CHANNEL_ID = "ndi_screen_share"
        private const val NOTIFICATION_ID = 4107
        private const val EXTRA_STREAM_NAME = "extra_stream_name"
        private const val ACTION_START = "com.ndi.sdkbridge.action.START_SCREEN_SHARE"
        private const val ACTION_STOP = "com.ndi.sdkbridge.action.STOP_SCREEN_SHARE"
        private const val START_TIMEOUT_MS = 1500L

        @Volatile
        private var startedLatch: CountDownLatch? = null

        fun start(context: Context, streamName: String) {
            val latch = CountDownLatch(1)
            startedLatch = latch
            val intent = Intent(context, ScreenShareForegroundService::class.java).apply {
                action = ACTION_START
                putExtra(EXTRA_STREAM_NAME, streamName)
            }
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                context.startForegroundService(intent)
            } else {
                context.startService(intent)
            }

            check(latch.await(START_TIMEOUT_MS, TimeUnit.MILLISECONDS)) {
                "Timed out waiting for screen share foreground service startup"
            }
        }

        fun stop(context: Context) {
            startedLatch = null
            context.stopService(Intent(context, ScreenShareForegroundService::class.java).apply {
                action = ACTION_STOP
            })
        }

        private fun signalStarted() {
            startedLatch?.countDown()
        }
    }
}

private object ServiceInfoCompat {
    const val FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION = 0x00000020
}