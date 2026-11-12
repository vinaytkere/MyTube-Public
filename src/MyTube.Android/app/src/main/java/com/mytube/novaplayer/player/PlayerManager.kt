package com.mytube.novaplayer.player

import android.content.ContentUris
import android.content.Context
import android.net.Uri
import android.os.Build
import android.provider.MediaStore
import android.util.Log
import androidx.annotation.OptIn
import androidx.media3.common.AudioAttributes
import androidx.media3.common.C
import androidx.media3.common.MediaItem
import androidx.media3.common.MimeTypes
import androidx.media3.common.PlaybackException
import androidx.media3.common.PlaybackParameters
import androidx.media3.common.Player
import androidx.media3.common.Tracks
import androidx.media3.common.util.UnstableApi
import androidx.media3.exoplayer.ExoPlayer
import androidx.media3.exoplayer.source.DefaultMediaSourceFactory
import androidx.media3.extractor.DefaultExtractorsFactory
import androidx.media3.ui.PlayerView
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import java.io.File

/**
 * PlayerManager backed by AndroidX Media3 (ExoPlayer).
 * Provides robust hardware-accelerated playback for local files, YouTube, HLS, DASH, and audio streams.
 */
class PlayerManager(private val context: Context) {

    private val coroutineScope = CoroutineScope(Dispatchers.Main + Job())
    private var progressJob: Job? = null

    private val _isPlaying = MutableStateFlow(false)
    val isPlaying: StateFlow<Boolean> = _isPlaying.asStateFlow()

    private val _currentTimeMs = MutableStateFlow(0L)
    val currentTimeMs: StateFlow<Long> = _currentTimeMs.asStateFlow()

    private val _durationMs = MutableStateFlow(0L)
    val durationMs: StateFlow<Long> = _durationMs.asStateFlow()

    private val _playbackSpeed = MutableStateFlow(1.0f)
    val playbackSpeed: StateFlow<Float> = _playbackSpeed.asStateFlow()

    private val _volume = MutableStateFlow(100) // 0 - 100 (Default 100% full unattenuated volume)
    val volume: StateFlow<Int> = _volume.asStateFlow()

    private val _isAudioOnlyMode = MutableStateFlow(false)
    val isAudioOnlyMode: StateFlow<Boolean> = _isAudioOnlyMode.asStateFlow()

    var onEndReachedListener: (() -> Unit)? = null

    private var currentPlayerView: PlayerView? = null

    private val playerListener = object : Player.Listener {
        override fun onIsPlayingChanged(isPlaying: Boolean) {
            _isPlaying.value = isPlaying
            if (isPlaying) {
                startProgressTracker()
            } else {
                stopProgressTracker()
            }
        }

        override fun onPlaybackStateChanged(playbackState: Int) {
            val stateName = when (playbackState) {
                Player.STATE_IDLE -> "STATE_IDLE"
                Player.STATE_BUFFERING -> "STATE_BUFFERING"
                Player.STATE_READY -> "STATE_READY"
                Player.STATE_ENDED -> "STATE_ENDED"
                else -> "STATE_$playbackState"
            }
            Log.i("NOVA_DEBUG", "ExoPlayer state: $stateName, pos=${exoPlayer.currentPosition}, dur=${exoPlayer.duration}")
            when (playbackState) {
                Player.STATE_READY -> {
                    val duration = exoPlayer.duration
                    _durationMs.value = if (duration > 0) duration else 0L
                    _currentTimeMs.value = exoPlayer.currentPosition.coerceAtLeast(0L)
                }
                Player.STATE_ENDED -> {
                    Log.i("NOVA_DEBUG", "ExoPlayer reached STATE_ENDED, triggering onTrackEndReached()")
                    _isPlaying.value = false
                    stopProgressTracker()
                    onTrackEndReached()
                }
                Player.STATE_IDLE, Player.STATE_BUFFERING -> {}
            }
        }

        override fun onPositionDiscontinuity(
            oldPosition: Player.PositionInfo,
            newPosition: Player.PositionInfo,
            reason: Int
        ) {
            _currentTimeMs.value = exoPlayer.currentPosition.coerceAtLeast(0L)
        }

        override fun onPlaybackParametersChanged(playbackParameters: PlaybackParameters) {
            _playbackSpeed.value = playbackParameters.speed
        }

        override fun onPlayerError(error: PlaybackException) {
            Log.e("NOVA_DEBUG", "ExoPlayer ERROR [${error.errorCodeName} / ${error.errorCode}]: ${error.message}", error)
            error.cause?.let { cause ->
                Log.e("NOVA_DEBUG", "ExoPlayer underlying cause: ${cause.javaClass.simpleName}: ${cause.message}", cause)
            }
            _isPlaying.value = false
            stopProgressTracker()
        }
    }

    @OptIn(UnstableApi::class)
    val exoPlayer: ExoPlayer by lazy {
        val extractorsFactory = DefaultExtractorsFactory().apply {
            setConstantBitrateSeekingEnabled(true)
        }
        val mediaSourceFactory = DefaultMediaSourceFactory(context, extractorsFactory)
        val audioAttributes = AudioAttributes.Builder()
            .setContentType(C.AUDIO_CONTENT_TYPE_MUSIC)
            .setUsage(C.USAGE_MEDIA)
            .build()

        ExoPlayer.Builder(context)
            .setMediaSourceFactory(mediaSourceFactory)
            .setAudioAttributes(audioAttributes, true) // handle audio focus
            .setHandleAudioBecomingNoisy(true)
            .setWakeMode(C.WAKE_MODE_LOCAL)
            .build().apply {
                addListener(playerListener)
                volume = _volume.value / 100f
            }
    }

    private fun startProgressTracker() {
        progressJob?.cancel()
        progressJob = coroutineScope.launch {
            while (isActive) {
                if (exoPlayer.isPlaying) {
                    _currentTimeMs.value = exoPlayer.currentPosition.coerceAtLeast(0L)
                    val duration = exoPlayer.duration
                    if (duration > 0 && duration != _durationMs.value) {
                        _durationMs.value = duration
                    }
                }
                delay(250)
            }
        }
    }

    private fun stopProgressTracker() {
        progressJob?.cancel()
        progressJob = null
    }

    fun setAudioOnlyMode(enabled: Boolean) {
        if (_isAudioOnlyMode.value == enabled && (!enabled && currentPlayerView != null)) return
        _isAudioOnlyMode.value = enabled
        if (enabled) {
            try {
                currentPlayerView?.player = null
                exoPlayer.clearVideoSurface()
            } catch (e: Exception) {
                Log.e("PlayerManager", "Error setting audio-only mode", e)
            }
        } else {
            currentPlayerView?.let { view ->
                try {
                    view.player = exoPlayer
                } catch (e: Exception) {
                    Log.e("PlayerManager", "Error restoring video surface", e)
                }
            }
        }
    }

    fun toggleAudioOnlyMode() {
        setAudioOnlyMode(!_isAudioOnlyMode.value)
    }

    fun attachPlayerView(playerView: PlayerView) {
        currentPlayerView = playerView
        if (!_isAudioOnlyMode.value) {
            if (playerView.player !== exoPlayer) {
                playerView.player = exoPlayer
            }
        } else {
            if (playerView.player != null) {
                playerView.player = null
            }
        }
    }

    fun detachPlayerView(playerView: PlayerView? = null) {
        try {
            if (playerView == null || currentPlayerView === playerView) {
                currentPlayerView?.player = null
                currentPlayerView = null
            }
        } catch (e: Exception) {
            Log.e("PlayerManager", "Error detaching PlayerView", e)
        }
    }

    fun detachVideoLayout() {
        detachPlayerView()
    }

    fun playMedia(
        mediaUri: String,
        filePath: String? = null,
        isNetworkStream: Boolean = false,
        mimeType: String? = null
    ) {
        Log.i("NOVA_DEBUG", "PlayerManager.playMedia called: mediaUri='$mediaUri', filePath='$filePath', mimeType='$mimeType'")
        try {
            val uri: Uri = when {
                // Priority 1: Already a valid MediaStore content URI -> Always use directly via ContentResolver
                mediaUri.startsWith("content://") -> {
                    Uri.parse(mediaUri)
                }
                // Priority 2: Remote / network stream
                isNetworkStream || mediaUri.startsWith("http://") || mediaUri.startsWith("https://") || mediaUri.startsWith("rtsp://") -> {
                    Uri.parse(mediaUri)
                }
                // Priority 3: Resolve local file path or file:// URI to MediaStore content URI first for Scoped Storage compatibility
                else -> {
                    val rawPath = filePath?.takeIf { it.isNotBlank() }
                        ?: if (mediaUri.startsWith("file://")) Uri.parse(mediaUri).path else mediaUri
                    val resolvedContentUri = rawPath?.let { resolveToContentUri(it) }
                    when {
                        resolvedContentUri != null -> resolvedContentUri
                        rawPath != null && File(rawPath).exists() && File(rawPath).canRead() -> Uri.fromFile(File(rawPath))
                        else -> Uri.parse(mediaUri)
                    }
                }
            }
            Log.i("NOVA_DEBUG", "PlayerManager.playMedia: resolved uri='$uri'")

            val mediaItemBuilder = MediaItem.Builder().setUri(uri)
            val effectiveMime = mimeType?.takeIf { it.isNotBlank() } ?: getMimeTypeFromUriOrPath(uri, filePath ?: mediaUri)
            if (!effectiveMime.isNullOrBlank()) {
                when (effectiveMime) {
                    MimeTypes.APPLICATION_M3U8,
                    MimeTypes.APPLICATION_MPD,
                    MimeTypes.APPLICATION_SS,
                    MimeTypes.APPLICATION_RTSP -> mediaItemBuilder.setMimeType(effectiveMime)
                    else -> {
                        if (mediaUri.startsWith("http://") || mediaUri.startsWith("https://")) {
                            mediaItemBuilder.setMimeType(effectiveMime)
                        }
                    }
                }
            }
            val mediaItem = mediaItemBuilder.build()

            if (!_isAudioOnlyMode.value && currentPlayerView != null) {
                if (currentPlayerView?.player !== exoPlayer) {
                    currentPlayerView?.player = exoPlayer
                }
            }
            exoPlayer.setMediaItem(mediaItem, true)
            exoPlayer.prepare()
            exoPlayer.play()
            _isPlaying.value = true
            Log.i("NOVA_DEBUG", "PlayerManager.playMedia: exoPlayer.play() invoked successfully for uri='$uri'")
            startProgressTracker()
        } catch (e: Exception) {
            Log.e("NOVA_DEBUG", "PlayerManager: Failed to start media playback for $mediaUri", e)
            _isPlaying.value = false
        }
    }

    private fun resolveToContentUri(path: String): Uri? {
        return try {
            val file = File(path)
            val ext = file.extension.lowercase()
            val videoExtensions = setOf("mp4", "mkv", "webm", "avi", "mov", "flv", "3gp", "ts", "m4v", "wmv")
            val isVideo = ext in videoExtensions
            val collection = if (isVideo) {
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) MediaStore.Video.Media.getContentUri(MediaStore.VOLUME_EXTERNAL)
                else MediaStore.Video.Media.EXTERNAL_CONTENT_URI
            } else {
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) MediaStore.Audio.Media.getContentUri(MediaStore.VOLUME_EXTERNAL)
                else MediaStore.Audio.Media.EXTERNAL_CONTENT_URI
            }
            val projection = arrayOf(MediaStore.MediaColumns._ID)
            val selection = "${MediaStore.MediaColumns.DATA} = ?"
            val selectionArgs = arrayOf(path)
            context.contentResolver.query(collection, projection, selection, selectionArgs, null)?.use { cursor ->
                if (cursor.moveToFirst()) {
                    val id = cursor.getLong(cursor.getColumnIndexOrThrow(MediaStore.MediaColumns._ID))
                    ContentUris.withAppendedId(collection, id)
                } else null
            }
        } catch (e: Exception) {
            Log.w("PlayerManager", "Failed to resolve content URI for path: $path", e)
            null
        }
    }

    private fun getMimeTypeFromUriOrPath(uri: Uri, pathOrUri: String): String? {
        if (uri.scheme == "content") {
            try {
                val type = context.contentResolver.getType(uri)
                if (!type.isNullOrBlank()) return type
            } catch (_: Exception) {}
        }
        val ext = File(pathOrUri).extension.lowercase()
        return when (ext) {
            "mp3" -> MimeTypes.AUDIO_MPEG
            "m4a", "aac" -> MimeTypes.AUDIO_AAC
            "flac" -> MimeTypes.AUDIO_FLAC
            "wav" -> MimeTypes.AUDIO_WAV
            "ogg" -> MimeTypes.AUDIO_OGG
            "opus" -> MimeTypes.AUDIO_OPUS
            "mp4", "m4v" -> MimeTypes.VIDEO_MP4
            "mkv" -> MimeTypes.VIDEO_MATROSKA
            "webm" -> MimeTypes.VIDEO_WEBM
            else -> null
        }
    }

    fun togglePlayPause() {
        if (exoPlayer.isPlaying) {
            pause()
        } else {
            play()
        }
    }

    fun play() {
        exoPlayer.play()
        _isPlaying.value = true
        startProgressTracker()
    }

    fun pause() {
        exoPlayer.pause()
        _isPlaying.value = false
        stopProgressTracker()
    }

    fun stop() {
        val listener = onEndReachedListener
        onEndReachedListener = null
        exoPlayer.stop()
        exoPlayer.clearMediaItems()
        _isPlaying.value = false
        _currentTimeMs.value = 0L
        stopProgressTracker()
        onEndReachedListener = listener
    }

    fun seekTo(timeMs: Long) {
        val safeTime = timeMs.coerceAtLeast(0L)
        exoPlayer.seekTo(safeTime)
        _currentTimeMs.value = safeTime
    }

    fun seekRelative(deltaMs: Long) {
        val newTime = (exoPlayer.currentPosition + deltaMs).coerceIn(0L, exoPlayer.duration.coerceAtLeast(0L))
        seekTo(newTime)
    }

    fun setSpeed(speed: Float) {
        val clamped = speed.coerceIn(0.25f, 3.0f)
        exoPlayer.setPlaybackSpeed(clamped)
        _playbackSpeed.value = clamped
    }

    fun setVolume(vol: Int) {
        val clamped = vol.coerceIn(0, 100)
        val floatVol = clamped / 100f
        exoPlayer.volume = floatVol
        _volume.value = clamped
    }

    @OptIn(UnstableApi::class)
    fun getAudioTrackCount(): Int {
        val tracks: Tracks = exoPlayer.currentTracks
        return tracks.groups.count { it.type == C.TRACK_TYPE_AUDIO }
    }

    @OptIn(UnstableApi::class)
    fun getSubtitleTrackCount(): Int {
        val tracks: Tracks = exoPlayer.currentTracks
        return tracks.groups.count { it.type == C.TRACK_TYPE_TEXT }
    }

    private fun onTrackEndReached() {
        _isPlaying.value = false
        onEndReachedListener?.invoke()
    }

    fun release() {
        stopProgressTracker()
        detachPlayerView()
        exoPlayer.removeListener(playerListener)
        exoPlayer.release()
    }
}
