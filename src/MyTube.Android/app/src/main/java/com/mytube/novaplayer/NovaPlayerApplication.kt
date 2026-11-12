package com.mytube.novaplayer

import android.app.Application
import android.app.NotificationChannel
import android.app.NotificationManager
import android.os.Build
import coil.ImageLoader
import coil.ImageLoaderFactory
import coil.decode.VideoFrameDecoder

class NovaPlayerApplication : Application(), ImageLoaderFactory {

    companion object {
        const val CHANNEL_PLAYBACK_ID = "nova_playback_channel"
        const val CHANNEL_DOWNLOADS_ID = "nova_downloads_channel"
    }

    val playerManager: com.mytube.novaplayer.player.PlayerManager by lazy {
        com.mytube.novaplayer.player.PlayerManager(this)
    }

    val queueManager: com.mytube.novaplayer.player.QueueManager by lazy {
        com.mytube.novaplayer.player.QueueManager(playerManager)
    }

    val sleepTimerManager: com.mytube.novaplayer.player.SleepTimerManager by lazy {
        com.mytube.novaplayer.player.SleepTimerManager(playerManager)
    }

    override fun onCreate() {
        super.onCreate()
        createNotificationChannels()
    }

    override fun newImageLoader(): ImageLoader {
        return ImageLoader.Builder(this)
            .components {
                add(VideoFrameDecoder.Factory())
            }
            .crossfade(true)
            .build()
    }

    private fun createNotificationChannels() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val notificationManager = getSystemService(NotificationManager::class.java)

            // Playback Channel
            val playbackChannel = NotificationChannel(
                CHANNEL_PLAYBACK_ID,
                getString(R.string.playback_channel_name),
                NotificationManager.IMPORTANCE_LOW
            ).apply {
                description = getString(R.string.playback_channel_description)
                setShowBadge(false)
            }

            // Downloads Channel
            val downloadsChannel = NotificationChannel(
                CHANNEL_DOWNLOADS_ID,
                getString(R.string.downloads_channel_name),
                NotificationManager.IMPORTANCE_LOW
            ).apply {
                description = getString(R.string.downloads_channel_description)
            }

            notificationManager?.createNotificationChannel(playbackChannel)
            notificationManager?.createNotificationChannel(downloadsChannel)
        }
    }
}
