package com.mytube.novaplayer.player

import android.app.Notification
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.graphics.Bitmap
import android.os.Binder
import android.os.Build
import android.os.IBinder
import android.os.PowerManager
import android.util.Log
import android.support.v4.media.MediaMetadataCompat
import android.support.v4.media.session.MediaSessionCompat
import android.support.v4.media.session.PlaybackStateCompat
import androidx.core.app.NotificationCompat
import com.mytube.novaplayer.NovaPlayerApplication
import com.mytube.novaplayer.R
import com.mytube.novaplayer.data.model.MediaItem
import com.mytube.novaplayer.data.util.MediaThumbnailHelper
import com.mytube.novaplayer.ui.MainActivity
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.launch

class PlaybackService : Service() {

    companion object {
        const val NOTIFICATION_ID = 1001
        const val ACTION_PLAY_PAUSE = "com.mytube.novaplayer.ACTION_PLAY_PAUSE"
        const val ACTION_PLAY = "com.mytube.novaplayer.ACTION_PLAY"
        const val ACTION_PAUSE = "com.mytube.novaplayer.ACTION_PAUSE"
        const val ACTION_NEXT = "com.mytube.novaplayer.ACTION_NEXT"
        const val ACTION_PREVIOUS = "com.mytube.novaplayer.ACTION_PREVIOUS"
        const val ACTION_STOP = "com.mytube.novaplayer.ACTION_STOP"
        const val ACTION_TOGGLE_AUDIO_MODE = "com.mytube.novaplayer.ACTION_TOGGLE_AUDIO_MODE"
    }

    private val binder = LocalBinder()
    lateinit var playerManager: PlayerManager
        private set
    lateinit var queueManager: QueueManager
        private set
    lateinit var sleepTimerManager: SleepTimerManager
        private set

    private var mediaSession: MediaSessionCompat? = null
    private val serviceScope = CoroutineScope(Dispatchers.Main + Job())
    private var wakeLock: PowerManager.WakeLock? = null

    private var currentArtworkMediaId: Long = -1L
    private var currentArtworkBitmap: Bitmap? = null

    inner class LocalBinder : Binder() {
        fun getService(): PlaybackService = this@PlaybackService
    }

    override fun onCreate() {
        super.onCreate()
        val app = application as NovaPlayerApplication
        playerManager = app.playerManager
        queueManager = app.queueManager
        sleepTimerManager = app.sleepTimerManager

        try {
            val powerManager = getSystemService(Context.POWER_SERVICE) as PowerManager
            wakeLock = powerManager.newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "NovaPlayer:PlaybackWakeLock")
        } catch (e: Exception) {
            e.printStackTrace()
        }

        initMediaSession()

        // Observe playback state, current media, and audio-only mode
        serviceScope.launch {
            combine(
                queueManager.currentMedia,
                playerManager.isPlaying,
                playerManager.isAudioOnlyMode
            ) { media, playing, audioOnly ->
                Triple(media, playing, audioOnly)
            }.collect { (media, playing, _) ->
                // WakeLock management for robust screen-off background playback
                if (playing) {
                    if (wakeLock?.isHeld != true) {
                        try {
                            wakeLock?.acquire(12 * 60 * 60 * 1000L) // 12h max safeguard
                        } catch (_: Exception) {}
                    }
                } else {
                    if (wakeLock?.isHeld == true) {
                        try {
                            wakeLock?.release()
                        } catch (_: Exception) {}
                    }
                }

                if (media != null) {
                    // Check if artwork needs to be decoded for newly loaded media
                    if (currentArtworkMediaId != media.id) {
                        currentArtworkMediaId = media.id
                        currentArtworkBitmap = null
                        serviceScope.launch {
                            val bitmap = MediaThumbnailHelper.getThumbnailBitmap(applicationContext, media)
                            if (currentArtworkMediaId == media.id) {
                                currentArtworkBitmap = bitmap
                                updateMediaSession(media, playerManager.isPlaying.value, bitmap)
                                val notif = buildNotification(media, playerManager.isPlaying.value, bitmap)
                                val manager = getSystemService(NOTIFICATION_SERVICE) as NotificationManager
                                manager.notify(NOTIFICATION_ID, notif)
                            }
                        }
                    }

                    updateMediaSession(media, playing, currentArtworkBitmap)
                    val notification = buildNotification(media, playing, currentArtworkBitmap)
                    if (playing) {
                        try {
                            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                                startForeground(NOTIFICATION_ID, notification, ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PLAYBACK)
                            } else {
                                startForeground(NOTIFICATION_ID, notification)
                            }
                        } catch (e: Exception) {
                            val manager = getSystemService(NOTIFICATION_SERVICE) as NotificationManager
                            manager.notify(NOTIFICATION_ID, notification)
                        }
                    } else {
                        val manager = getSystemService(NOTIFICATION_SERVICE) as NotificationManager
                        manager.notify(NOTIFICATION_ID, notification)
                    }
                } else {
                    stopForeground(STOP_FOREGROUND_REMOVE)
                }
            }
        }
    }

    private fun initMediaSession() {
        mediaSession = MediaSessionCompat(this, "NovaPlaybackSession").apply {
            setCallback(object : MediaSessionCompat.Callback() {
                override fun onPlay() {
                    playerManager.togglePlayPause()
                }

                override fun onPause() {
                    playerManager.pause()
                }

                override fun onSkipToNext() {
                    queueManager.playNext()
                }

                override fun onSkipToPrevious() {
                    queueManager.playPrevious()
                }

                override fun onStop() {
                    Log.i("NOVA_DEBUG", "MediaSessionCompat.Callback.onStop() triggered")
                    handleStop("MediaSession.onStop")
                }

                override fun onSeekTo(pos: Long) {
                    playerManager.seekTo(pos)
                }
            })
            isActive = true
        }
    }

    private fun updateMediaSession(media: MediaItem, isPlaying: Boolean, artwork: Bitmap?) {
        val state = PlaybackStateCompat.Builder()
            .setActions(
                PlaybackStateCompat.ACTION_PLAY or
                PlaybackStateCompat.ACTION_PAUSE or
                PlaybackStateCompat.ACTION_PLAY_PAUSE or
                PlaybackStateCompat.ACTION_STOP or
                PlaybackStateCompat.ACTION_SKIP_TO_NEXT or
                PlaybackStateCompat.ACTION_SKIP_TO_PREVIOUS or
                PlaybackStateCompat.ACTION_SEEK_TO
            )
            .setState(
                if (isPlaying) PlaybackStateCompat.STATE_PLAYING else PlaybackStateCompat.STATE_PAUSED,
                playerManager.currentTimeMs.value,
                1.0f
            )
            .build()
        mediaSession?.setPlaybackState(state)

        val metadataBuilder = MediaMetadataCompat.Builder()
            .putString(MediaMetadataCompat.METADATA_KEY_TITLE, media.title)
            .putString(MediaMetadataCompat.METADATA_KEY_ARTIST, media.artistOrSubtitle)
            .putLong(MediaMetadataCompat.METADATA_KEY_DURATION, media.durationMs)

        if (artwork != null) {
            metadataBuilder.putBitmap(MediaMetadataCompat.METADATA_KEY_ALBUM_ART, artwork)
            metadataBuilder.putBitmap(MediaMetadataCompat.METADATA_KEY_ART, artwork)
        }

        mediaSession?.setMetadata(metadataBuilder.build())
    }

    private fun buildNotification(media: MediaItem, isPlaying: Boolean, artwork: Bitmap?): Notification {
        val openAppIntent = Intent(this, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_SINGLE_TOP
        }
        val openAppPendingIntent = PendingIntent.getActivity(
            this,
            0,
            openAppIntent,
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        )

        val prevPendingIntent = PendingIntent.getService(
            this, 1,
            Intent(this, PlaybackService::class.java).apply { action = ACTION_PREVIOUS },
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        )
        val playPausePendingIntent = PendingIntent.getService(
            this, 2,
            Intent(this, PlaybackService::class.java).apply { action = ACTION_PLAY_PAUSE },
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        )
        val nextPendingIntent = PendingIntent.getService(
            this, 3,
            Intent(this, PlaybackService::class.java).apply { action = ACTION_NEXT },
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        )
        val stopPendingIntent = PendingIntent.getService(
            this, 4,
            Intent(this, PlaybackService::class.java).apply { action = ACTION_STOP },
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        )

        val mediaStyle = androidx.media.app.NotificationCompat.MediaStyle()
            .setMediaSession(mediaSession?.sessionToken)
            .setShowActionsInCompactView(0, 1, 2)

        val isAudioMode = playerManager.isAudioOnlyMode.value
        val subText = if (media.isVideo && isAudioMode) "Audio Mode • NovaPlayer" else "NovaPlayer"

        val builder = NotificationCompat.Builder(this, NovaPlayerApplication.CHANNEL_PLAYBACK_ID)
            .setSmallIcon(R.drawable.ic_notification_play)
            .setContentTitle(media.title)
            .setContentText(media.artistOrSubtitle)
            .setSubText(subText)
            .setContentIntent(openAppPendingIntent)
            .setVisibility(NotificationCompat.VISIBILITY_PUBLIC)
            .setOngoing(isPlaying)
            .setStyle(mediaStyle)
            .addAction(R.drawable.ic_notification_previous, "Previous", prevPendingIntent)
            .addAction(
                if (isPlaying) R.drawable.ic_notification_pause else R.drawable.ic_notification_play,
                if (isPlaying) "Pause" else "Play",
                playPausePendingIntent
            )
            .addAction(R.drawable.ic_notification_next, "Next", nextPendingIntent)
            .addAction(R.drawable.ic_notification_stop, "Stop", stopPendingIntent)

        if (artwork != null) {
            builder.setLargeIcon(artwork)
        }

        return builder.build()
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        val action = intent?.action
        Log.i("NOVA_DEBUG", "PlaybackService.onStartCommand: action=$action, flags=$flags, startId=$startId")
        when (action) {
            ACTION_PLAY_PAUSE -> {
                playerManager.togglePlayPause()
            }
            ACTION_PLAY -> {
                if (!playerManager.isPlaying.value) playerManager.togglePlayPause()
            }
            ACTION_PAUSE -> {
                playerManager.pause()
            }
            ACTION_NEXT -> {
                queueManager.playNext()
            }
            ACTION_PREVIOUS -> {
                queueManager.playPrevious()
            }
            ACTION_STOP -> {
                handleStop("ACTION_STOP")
            }
            ACTION_TOGGLE_AUDIO_MODE -> {
                playerManager.toggleAudioOnlyMode()
            }
        }
        return START_NOT_STICKY
    }

    private fun handleStop(source: String = "unknown") {
        Log.i("NOVA_DEBUG", "PlaybackService.handleStop() invoked from source='$source'. Pausing player and stopping foreground.")
        playerManager.pause()
        if (wakeLock?.isHeld == true) {
            try { wakeLock?.release() } catch (_: Exception) {}
        }
        stopForeground(STOP_FOREGROUND_REMOVE)
        val manager = getSystemService(NOTIFICATION_SERVICE) as NotificationManager
        manager.cancel(NOTIFICATION_ID)
        stopSelf()
    }

    override fun onBind(intent: Intent?): IBinder = binder

    override fun onDestroy() {
        super.onDestroy()
        if (wakeLock?.isHeld == true) {
            try { wakeLock?.release() } catch (_: Exception) {}
        }
        serviceScope.cancel()
        mediaSession?.release()
    }
}

