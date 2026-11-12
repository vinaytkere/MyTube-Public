package com.mytube.novaplayer.player

import android.util.Log
import com.mytube.novaplayer.data.model.MediaItem
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

enum class PlaybackMode {
    PLAY_ALL,
    PLAY_SINGLE,
    PLAY_SELECTED,
    LOOP_PLAYLIST,
    SHUFFLE
}

class QueueManager(private val playerManager: PlayerManager) {

    private val _playlist = MutableStateFlow<List<MediaItem>>(emptyList())
    val playlist: StateFlow<List<MediaItem>> = _playlist.asStateFlow()

    private val _currentIndex = MutableStateFlow(-1)
    val currentIndex: StateFlow<Int> = _currentIndex.asStateFlow()

    private val _currentMedia = MutableStateFlow<MediaItem?>(null)
    val currentMedia: StateFlow<MediaItem?> = _currentMedia.asStateFlow()

    private val _playbackMode = MutableStateFlow(PlaybackMode.PLAY_ALL)
    val playbackMode: StateFlow<PlaybackMode> = _playbackMode.asStateFlow()

    // Playlist-wide audio mode toggle for videos
    private val _isPlaylistAudioMode = MutableStateFlow(false)
    val isPlaylistAudioMode: StateFlow<Boolean> = _isPlaylistAudioMode.asStateFlow()

    // Tracks per-playlist audio mode settings
    private val playlistAudioModeSettings = mutableMapOf<Long, Boolean>()

    private val selectedIds = mutableSetOf<Long>()

    init {
        playerManager.onEndReachedListener = {
            handleEndReached()
        }
    }

    fun isPlaylistAudioModeFor(playlistId: Long): Boolean {
        return playlistAudioModeSettings[playlistId] ?: false
    }

    fun selectPlaylist(playlistId: Long?) {
        if (playlistId != null) {
            _isPlaylistAudioMode.value = playlistAudioModeSettings[playlistId] ?: false
        }
    }

    fun setPlaylistAudioMode(playlistId: Long?, enabled: Boolean) {
        if (playlistId != null) {
            playlistAudioModeSettings[playlistId] = enabled
        }
        _isPlaylistAudioMode.value = enabled
        val current = _currentMedia.value
        if (current?.isVideo == true) {
            playerManager.setAudioOnlyMode(enabled)
        }
    }

    /**
     * Plays an individual track outside a playlist context (e.g. from Library or Downloads).
     * Audio files play in audio mode; videos play in video mode.
     */
    fun setSingleTrack(item: MediaItem) {
        Log.i("NOVA_DEBUG", "QueueManager.setSingleTrack: '${item.title}' (id=${item.id}, isVideo=${item.isVideo}, uri=${item.uri})")
        _isPlaylistAudioMode.value = false
        _playlist.value = listOf(item)
        _currentIndex.value = 0
        _currentMedia.value = item

        playerManager.setAudioOnlyMode(!item.isVideo)
        playerManager.playMedia(item.uri, filePath = item.path, mimeType = item.mimeType)
    }

    fun setQueue(
        items: List<MediaItem>,
        startIndex: Int = 0,
        autoPlay: Boolean = true,
        isPlaylistContext: Boolean = false,
        playlistId: Long? = null
    ) {
        Log.i("NOVA_DEBUG", "QueueManager.setQueue: count=${items.size}, startIndex=$startIndex, autoPlay=$autoPlay, isPlaylistContext=$isPlaylistContext")
        _playlist.value = items

        if (playlistId != null) {
            _isPlaylistAudioMode.value = playlistAudioModeSettings[playlistId] ?: false
        } else {
            _isPlaylistAudioMode.value = false
        }

        val targetIdx = if (startIndex in items.indices) startIndex else 0
        if (items.isNotEmpty() && autoPlay) {
            playTrackAtIndex(targetIdx)
        }
    }

    fun setPlaybackMode(mode: PlaybackMode) {
        _playbackMode.value = mode
    }

    fun setSelectedIds(ids: Set<Long>) {
        selectedIds.clear()
        selectedIds.addAll(ids)
    }

    fun playTrackAtIndex(index: Int) {
        val list = _playlist.value
        Log.i("NOVA_DEBUG", "QueueManager.playTrackAtIndex: index=$index (queue size=${list.size})")
        if (index in list.indices) {
            _currentIndex.value = index
            val item = list[index]
            _currentMedia.value = item
            Log.i("NOVA_DEBUG", "QueueManager: _currentMedia assigned -> '${item.title}' (id=${item.id}, isVideo=${item.isVideo}, uri=${item.uri})")

            if (!item.isVideo) {
                // Audio files always play in audio mode
                playerManager.setAudioOnlyMode(true)
            } else if (_isPlaylistAudioMode.value) {
                // Playlist has audio mode enabled for all videos
                playerManager.setAudioOnlyMode(true)
            } else {
                // Default: Video plays in Video Mode
                playerManager.setAudioOnlyMode(false)
            }
            playerManager.playMedia(item.uri, filePath = item.path, mimeType = item.mimeType)
        } else {
            Log.w("NOVA_DEBUG", "QueueManager.playTrackAtIndex: index $index out of bounds for size ${list.size}")
        }
    }

    fun playNext() {
        val list = _playlist.value
        if (list.isEmpty()) return

        when (_playbackMode.value) {
            PlaybackMode.PLAY_SINGLE -> {
                playerManager.stop()
            }
            PlaybackMode.SHUFFLE -> {
                val nextIdx = list.indices.random()
                playTrackAtIndex(nextIdx)
            }
            PlaybackMode.PLAY_SELECTED -> {
                val nextSelectedIdx = list.indices.drop(_currentIndex.value + 1).firstOrNull { idx ->
                    selectedIds.contains(list[idx].id)
                } ?: -1
                if (nextSelectedIdx != -1) {
                    playTrackAtIndex(nextSelectedIdx)
                } else {
                    playerManager.stop()
                }
            }
            PlaybackMode.LOOP_PLAYLIST -> {
                val nextIdx = (_currentIndex.value + 1) % list.size
                playTrackAtIndex(nextIdx)
            }
            PlaybackMode.PLAY_ALL -> {
                val nextIdx = _currentIndex.value + 1
                if (nextIdx < list.size) {
                    playTrackAtIndex(nextIdx)
                } else {
                    playerManager.stop()
                }
            }
        }
    }

    fun playPrevious() {
        val list = _playlist.value
        if (list.isEmpty()) return
        val prevIdx = (_currentIndex.value - 1).coerceAtLeast(0)
        playTrackAtIndex(prevIdx)
    }

    fun stopAndClear(source: String = "unknown") {
        Log.w("NOVA_DEBUG", "QueueManager.stopAndClear() called by source='$source'!\n${Log.getStackTraceString(Throwable())}")
        _playlist.value = emptyList()
        _currentMedia.value = null
        _currentIndex.value = -1
        _isPlaylistAudioMode.value = false
        playerManager.stop()
    }

    fun reorderQueue(fromIndex: Int, toIndex: Int) {
        val list = _playlist.value.toMutableList()
        if (fromIndex in list.indices && toIndex in list.indices) {
            val item = list.removeAt(fromIndex)
            list.add(toIndex, item)
            _playlist.value = list
            val cur = _currentMedia.value
            if (cur != null) {
                _currentIndex.value = list.indexOfFirst { it.id == cur.id }
            }
        }
    }

    fun setPlaylistTracks(items: List<MediaItem>) {
        _playlist.value = items
        val cur = _currentMedia.value
        if (cur != null) {
            _currentIndex.value = items.indexOfFirst { it.id == cur.id }
        }
    }

    private fun handleEndReached() {
        Log.i("NOVA_DEBUG", "QueueManager.handleEndReached() called! currentMedia='${_currentMedia.value?.title}', calling playNext()")
        playNext()
    }
}

