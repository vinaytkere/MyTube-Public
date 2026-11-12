package com.mytube.novaplayer.ui

import android.Manifest
import android.app.Activity
import android.app.RecoverableSecurityException
import android.content.ComponentName
import android.content.ContentUris
import android.content.Context
import android.content.Intent
import android.content.ServiceConnection
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.os.Environment
import android.os.IBinder
import android.provider.MediaStore
import android.util.Log
import androidx.activity.ComponentActivity
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.compose.setContent
import androidx.activity.result.IntentSenderRequest
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Scaffold
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.core.content.ContextCompat
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import com.mytube.novaplayer.core.theme.Background
import com.mytube.novaplayer.core.theme.NovaPlayerTheme
import com.mytube.novaplayer.data.local.NovaDatabase
import com.mytube.novaplayer.data.local.entity.PlaylistEntity
import com.mytube.novaplayer.data.local.entity.PlaylistTrackEntity
import com.mytube.novaplayer.data.local.entity.toEntity
import com.mytube.novaplayer.data.local.entity.toMediaItem
import com.mytube.novaplayer.data.model.MediaItem
import com.mytube.novaplayer.data.scanner.MediaStoreScanner
import com.mytube.novaplayer.NovaPlayerApplication
import com.mytube.novaplayer.player.PlaybackService
import com.mytube.novaplayer.player.PlayerManager
import com.mytube.novaplayer.player.QueueManager
import com.mytube.novaplayer.player.SleepTimerManager
import com.mytube.novaplayer.ui.components.BottomNavigationBar
import com.mytube.novaplayer.ui.components.MiniPlayerBar
import com.mytube.novaplayer.ui.navigation.Screen
import com.mytube.novaplayer.data.remote.DownloadRepository
import com.mytube.novaplayer.ui.screens.download.DownloadScreen
import com.mytube.novaplayer.ui.screens.library.MediaFilter
import com.mytube.novaplayer.ui.screens.library.MediaLibraryScreen
import com.mytube.novaplayer.ui.screens.player.PlayerDeckScreen
import com.mytube.novaplayer.ui.screens.playlists.PlaylistScreen
import com.mytube.novaplayer.ui.screens.tools.SleepTimerScreen
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.File

class MainActivity : ComponentActivity() {

    private var playbackService by mutableStateOf<PlaybackService?>(null)
    private var isBound by mutableStateOf(false)

    private val serviceConnection = object : ServiceConnection {
        override fun onServiceConnected(name: ComponentName?, service: IBinder?) {
            val binder = service as PlaybackService.LocalBinder
            playbackService = binder.getService()
            isBound = true
        }

        override fun onServiceDisconnected(name: ComponentName?) {
            playbackService = null
            isBound = false
        }
    }

    private val requestPermissionLauncher = registerForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions()
    ) { _ ->
        // Permissions handled
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        requestMediaPermissions()
        bindPlaybackService()

        setContent {
            NovaPlayerTheme {
                val navController = rememberNavController()
                val navBackStackEntry by navController.currentBackStackEntryAsState()
                val currentRoute = navBackStackEntry?.destination?.route ?: Screen.MediaLibrary.route

                val coroutineScope = rememberCoroutineScope()
                val scanner = remember { MediaStoreScanner(this@MainActivity) }
                val db = remember { NovaDatabase.getInstance(this@MainActivity) }
                val playlistDao = remember { db.playlistDao() }
                val downloadRepository = remember { DownloadRepository(this@MainActivity) }

                var mediaList by remember { mutableStateOf<List<MediaItem>>(emptyList()) }
                var selectedFilter by rememberSaveable { mutableStateOf(MediaFilter.ALL) }
                var selectedPlaylistId by rememberSaveable { mutableStateOf<Long?>(null) }

                // Playlists from Room DB
                val playlists by playlistDao.getAllPlaylists().collectAsState(initial = emptyList())

                // Tracks for the currently selected playlist
                val activePlaylistTracksEntities by remember(selectedPlaylistId, playlists) {
                    val targetId = selectedPlaylistId ?: playlists.firstOrNull()?.id ?: -1L
                    if (targetId != -1L) playlistDao.getTracksForPlaylist(targetId)
                    else flowOf(emptyList())
                }.collectAsState(initial = emptyList())

                val activePlaylistTracks = remember(activePlaylistTracksEntities) {
                    activePlaylistTracksEntities.map { it.toMediaItem() }
                }

                // Initial scan and database sync
                LaunchedEffect(Unit) {
                    val scanned = scanner.scanAllMedia()
                    mediaList = scanned
                    if (scanned.isNotEmpty()) {
                        withContext(Dispatchers.IO) {
                            db.mediaDao().insertAll(scanned.map { it.toEntity() })
                        }
                    }
                }

                // Create default playlist if none exists
                LaunchedEffect(playlists) {
                    if (playlists.isEmpty()) {
                        withContext(Dispatchers.IO) {
                            val defaultId = playlistDao.insertPlaylist(PlaylistEntity(name = "Favorites"))
                            selectedPlaylistId = defaultId
                        }
                    } else if (selectedPlaylistId == null) {
                        selectedPlaylistId = playlists.first().id
                    }
                }

                // Unified application-wide managers (single source of truth)
                val app = application as NovaPlayerApplication
                val activePlayerManager = app.playerManager
                val activeQueueManager = app.queueManager
                val activeSleepTimerManager = app.sleepTimerManager

                val currentMedia by activeQueueManager.currentMedia.collectAsState()
                val isPlaying by activePlayerManager.isPlaying.collectAsState()
                val isAudioOnlyMode by activePlayerManager.isAudioOnlyMode.collectAsState()
                val isPlaylistAudioMode by activeQueueManager.isPlaylistAudioMode.collectAsState()

                val effectivePlaylistId = selectedPlaylistId ?: playlists.firstOrNull()?.id

                LaunchedEffect(effectivePlaylistId) {
                    activeQueueManager.selectPlaylist(effectivePlaylistId)
                }

                // Scoped Storage deletion request launcher (Android 10+)
                val pendingDeleteItems = remember { mutableStateOf<List<MediaItem>>(emptyList()) }
                val deleteIntentLauncher = rememberLauncherForActivityResult(
                    contract = ActivityResultContracts.StartIntentSenderForResult()
                ) { result ->
                    if (result.resultCode == Activity.RESULT_OK) {
                        val targets = pendingDeleteItems.value
                        coroutineScope.launch {
                            withContext(Dispatchers.IO) {
                                targets.forEach { item ->
                                    try {
                                        db.mediaDao().deleteById(item.id)
                                    } catch (_: Exception) {}
                                    if (item.path.isNotBlank()) {
                                        try {
                                            val f = File(item.path)
                                            if (f.exists()) f.delete()
                                        } catch (_: Exception) {}
                                    }
                                }
                            }
                            mediaList = scanner.scanAllMedia()
                            pendingDeleteItems.value = emptyList()
                        }
                    } else {
                        pendingDeleteItems.value = emptyList()
                    }
                }

                // File deletion handler
                val onDeleteMediaItems: (List<MediaItem>) -> Unit = { targets ->
                    val currentlyPlaying = activeQueueManager.currentMedia.value
                    if (targets.any { it.id == currentlyPlaying?.id }) {
                        activeQueueManager.stopAndClear("MainActivity.onDeleteMediaItems")
                    }

                    val contentUris = targets.mapNotNull { getMediaContentUri(this@MainActivity, it) }

                    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                        if (Environment.isExternalStorageManager()) {
                            // Granted All Files Access -> delete directly
                            coroutineScope.launch {
                                withContext(Dispatchers.IO) {
                                    targets.forEach { item ->
                                        val uri = getMediaContentUri(this@MainActivity, item)
                                        if (uri != null) {
                                            try { contentResolver.delete(uri, null, null) } catch (_: Exception) {}
                                        }
                                        if (item.path.isNotBlank()) {
                                            try {
                                                val f = File(item.path)
                                                if (f.exists()) f.delete()
                                            } catch (_: Exception) {}
                                        }
                                        try { db.mediaDao().deleteById(item.id) } catch (_: Exception) {}
                                    }
                                }
                                mediaList = scanner.scanAllMedia()
                            }
                        } else if (contentUris.isNotEmpty()) {
                            // Android 11+ official Scoped Storage delete request
                            try {
                                pendingDeleteItems.value = targets
                                val pendingIntent = MediaStore.createDeleteRequest(contentResolver, contentUris)
                                deleteIntentLauncher.launch(
                                    IntentSenderRequest.Builder(pendingIntent.intentSender).build()
                                )
                            } catch (e: Exception) {
                                coroutineScope.launch {
                                    withContext(Dispatchers.IO) {
                                        targets.forEach { item ->
                                            val uri = getMediaContentUri(this@MainActivity, item)
                                            if (uri != null) {
                                                try { contentResolver.delete(uri, null, null) } catch (_: Exception) {}
                                            }
                                            if (item.path.isNotBlank()) {
                                                try {
                                                    val f = File(item.path)
                                                    if (f.exists()) f.delete()
                                                } catch (_: Exception) {}
                                            }
                                            try { db.mediaDao().deleteById(item.id) } catch (_: Exception) {}
                                        }
                                    }
                                    mediaList = scanner.scanAllMedia()
                                }
                            }
                        }
                    } else {
                        // Android 10 or below
                        coroutineScope.launch {
                            var recoverableSender: android.content.IntentSender? = null
                            withContext(Dispatchers.IO) {
                                targets.forEach { item ->
                                    val uri = getMediaContentUri(this@MainActivity, item)
                                    if (uri != null) {
                                        try {
                                            contentResolver.delete(uri, null, null)
                                        } catch (e: SecurityException) {
                                            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q && e is RecoverableSecurityException) {
                                                recoverableSender = e.userAction.actionIntent.intentSender
                                            }
                                        } catch (_: Exception) {}
                                    }
                                    if (item.path.isNotBlank()) {
                                        try {
                                            val f = File(item.path)
                                            if (f.exists()) f.delete()
                                        } catch (_: Exception) {}
                                    }
                                    try { db.mediaDao().deleteById(item.id) } catch (_: Exception) {}
                                }
                            }
                            if (recoverableSender != null) {
                                pendingDeleteItems.value = targets
                                deleteIntentLauncher.launch(
                                    IntentSenderRequest.Builder(recoverableSender!!).build()
                                )
                            } else {
                                mediaList = scanner.scanAllMedia()
                            }
                        }
                    }
                }

                val configuration = androidx.compose.ui.platform.LocalConfiguration.current
                val isLandscape = configuration.orientation == android.content.res.Configuration.ORIENTATION_LANDSCAPE
                val isNowPlaying = currentRoute == Screen.NowPlaying.route
                val shouldShowBottomBar = !isNowPlaying && !isLandscape

                Scaffold(
                    bottomBar = {
                        if (shouldShowBottomBar) {
                            Column {
                                // Persistent Mini Player Bar across all library screens
                                val playingMedia = currentMedia
                                if (playingMedia != null) {
                                    MiniPlayerBar(
                                        media = playingMedia,
                                        isPlaying = isPlaying,
                                        isAudioOnlyMode = isAudioOnlyMode,
                                        onTogglePlayPause = { activePlayerManager.togglePlayPause() },
                                        onStopAndClose = {
                                            activeQueueManager.stopAndClear("MiniPlayerBar.onStopAndClose")
                                        },
                                        onClickBar = {
                                            navController.navigate(Screen.NowPlaying.route)
                                        }
                                    )
                                }

                                BottomNavigationBar(
                                    currentRoute = currentRoute,
                                    onNavigate = { route ->
                                        navController.navigate(route) {
                                            popUpTo(Screen.MediaLibrary.route) { saveState = true }
                                            launchSingleTop = true
                                            restoreState = true
                                        }
                                    }
                                )
                            }
                        }
                    }
                ) { innerPadding ->
                    Box(
                        modifier = Modifier
                            .fillMaxSize()
                            .background(Background)
                            .padding(if (shouldShowBottomBar) innerPadding else PaddingValues(0.dp))
                    ) {
                        NavHost(
                            navController = navController,
                            startDestination = Screen.MediaLibrary.route
                        ) {
                            composable(Screen.MediaLibrary.route) {
                                MediaLibraryScreen(
                                    mediaList = mediaList,
                                    selectedFilter = selectedFilter,
                                    onSelectFilter = { selectedFilter = it },
                                    currentlyPlayingMediaId = currentMedia?.id,
                                    isPlaying = isPlaying,
                                    playlists = playlists,
                                    onPlayTrack = { track ->
                                        Log.i("NOVA_DEBUG", "MainActivity.onPlayTrack: Clicked track id=${track.id}, title='${track.title}', isVideo=${track.isVideo}, uri=${track.uri}")
                                        Log.i("NOVA_DEBUG", "MainActivity.onPlayTrack: currentMedia='${currentMedia?.title}' (id=${currentMedia?.id}), isPlaying=$isPlaying")
                                        try {
                                            val svcIntent = Intent(this@MainActivity, PlaybackService::class.java)
                                            startService(svcIntent)
                                        } catch (_: Exception) {}
                                        if (currentMedia?.id == track.id && isPlaying) {
                                            Log.i("NOVA_DEBUG", "MainActivity.onPlayTrack: track already actively playing, navigating to NowPlaying")
                                            navController.navigate(Screen.NowPlaying.route)
                                        } else {
                                            val currentList = if (track.isVideo) {
                                                val vids = mediaList.filter { it.isVideo }
                                                if (vids.isNotEmpty()) vids else listOf(track)
                                            } else {
                                                val auds = mediaList.filter { !it.isVideo }
                                                if (auds.isNotEmpty()) auds else listOf(track)
                                            }
                                            val trackIdx = currentList.indexOfFirst { it.id == track.id }
                                            Log.i("NOVA_DEBUG", "MainActivity.onPlayTrack: prepared currentList size=${currentList.size}, trackIdx=$trackIdx")
                                            if (trackIdx != -1) {
                                                activeQueueManager.setQueue(
                                                    items = currentList,
                                                    startIndex = trackIdx,
                                                    isPlaylistContext = false
                                                )
                                            } else {
                                                activeQueueManager.setSingleTrack(track)
                                            }
                                            navController.navigate(Screen.NowPlaying.route)
                                        }
                                    },
                                    onTogglePlayPause = {
                                        activePlayerManager.togglePlayPause()
                                    },
                                    onPlaySelected = { selected ->
                                        try {
                                            val svcIntent = Intent(this@MainActivity, PlaybackService::class.java)
                                            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                                                ContextCompat.startForegroundService(this@MainActivity, svcIntent)
                                            } else {
                                                startService(svcIntent)
                                            }
                                        } catch (_: Exception) {}
                                        activeQueueManager.setQueue(selected, 0, isPlaylistContext = false)
                                        navController.navigate(Screen.NowPlaying.route)
                                    },
                                    onRescan = {
                                        coroutineScope.launch {
                                            val scanned = scanner.scanAllMedia()
                                            mediaList = scanned
                                            withContext(Dispatchers.IO) {
                                                db.mediaDao().insertAll(scanned.map { it.toEntity() })
                                            }
                                        }
                                    },
                                    onDeleteItems = onDeleteMediaItems,
                                    onAddToPlaylist = { playlistId, items ->
                                        coroutineScope.launch {
                                            withContext(Dispatchers.IO) {
                                                db.mediaDao().insertAll(items.map { it.toEntity() })
                                                val trackEntities = items.mapIndexed { idx, itm ->
                                                    PlaylistTrackEntity(playlistId = playlistId, mediaId = itm.id, sortOrder = idx)
                                                }
                                                playlistDao.insertTracks(trackEntities)
                                            }
                                        }
                                    },
                                    onCreatePlaylistAndAdd = { name, items ->
                                        coroutineScope.launch {
                                            withContext(Dispatchers.IO) {
                                                db.mediaDao().insertAll(items.map { it.toEntity() })
                                                val newId = playlistDao.insertPlaylist(PlaylistEntity(name = name))
                                                val trackEntities = items.mapIndexed { idx, itm ->
                                                    PlaylistTrackEntity(playlistId = newId, mediaId = itm.id, sortOrder = idx)
                                                }
                                                playlistDao.insertTracks(trackEntities)
                                                selectedPlaylistId = newId
                                            }
                                        }
                                    }
                                )
                            }

                            composable(Screen.NowPlaying.route) {
                                PlayerDeckScreen(
                                    queueManager = activeQueueManager,
                                    playerManager = activePlayerManager,
                                    onBack = { navController.popBackStack() },
                                    onStop = {
                                        activePlayerManager.stop()
                                        navController.popBackStack()
                                    },
                                    onOpenTools = { navController.navigate(Screen.ToolsAndTimer.route) },
                                    onSkipNext = { activeQueueManager.playNext() },
                                    onSkipPrevious = { activeQueueManager.playPrevious() }
                                )
                            }

                            composable(Screen.Playlists.route) {
                                PlaylistScreen(
                                    playlists = playlists,
                                    selectedPlaylistId = effectivePlaylistId,
                                    onSelectPlaylist = { id ->
                                        selectedPlaylistId = id
                                        activeQueueManager.selectPlaylist(id)
                                    },
                                    tracks = activePlaylistTracks,
                                    allLibraryMedia = mediaList,
                                    isPlaylistAudioMode = isPlaylistAudioMode,
                                    onTogglePlaylistAudioMode = { enabled ->
                                        activeQueueManager.setPlaylistAudioMode(effectivePlaylistId, enabled)
                                    },
                                    onCreatePlaylist = { name ->
                                        coroutineScope.launch {
                                            withContext(Dispatchers.IO) {
                                                val newId = playlistDao.insertPlaylist(PlaylistEntity(name = name))
                                                selectedPlaylistId = newId
                                                activeQueueManager.selectPlaylist(newId)
                                            }
                                        }
                                    },
                                    onRenamePlaylist = { playlistId, newName ->
                                        coroutineScope.launch {
                                            withContext(Dispatchers.IO) {
                                                playlistDao.updatePlaylistName(playlistId, newName)
                                            }
                                        }
                                    },
                                    onDeletePlaylist = { playlistId ->
                                        coroutineScope.launch {
                                            withContext(Dispatchers.IO) {
                                                playlistDao.deletePlaylist(playlistId)
                                            }
                                            if (selectedPlaylistId == playlistId) {
                                                val remaining = playlists.filter { it.id != playlistId }
                                                selectedPlaylistId = remaining.firstOrNull()?.id
                                                activeQueueManager?.selectPlaylist(selectedPlaylistId)
                                            }
                                        }
                                    },
                                    onAddTracksToPlaylist = { playlistId, items ->
                                        coroutineScope.launch {
                                            withContext(Dispatchers.IO) {
                                                db.mediaDao().insertAll(items.map { it.toEntity() })
                                                val trackEntities = items.mapIndexed { idx, itm ->
                                                    PlaylistTrackEntity(playlistId = playlistId, mediaId = itm.id, sortOrder = idx)
                                                }
                                                playlistDao.insertTracks(trackEntities)
                                            }
                                        }
                                    },
                                    onRemoveTrackFromPlaylist = { playlistId, mediaId ->
                                        coroutineScope.launch {
                                            withContext(Dispatchers.IO) {
                                                playlistDao.removeTrackFromPlaylist(playlistId, mediaId)
                                            }
                                        }
                                    },
                                    onReorderTracks = { playlistId, reorderedItems ->
                                        coroutineScope.launch {
                                            withContext(Dispatchers.IO) {
                                                playlistDao.updateTrackOrder(playlistId, reorderedItems.map { it.id })
                                            }
                                            activeQueueManager.setPlaylistTracks(reorderedItems)
                                        }
                                    },
                                    onPlayAll = {
                                        try {
                                            val svcIntent = Intent(this@MainActivity, PlaybackService::class.java)
                                            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                                                ContextCompat.startForegroundService(this@MainActivity, svcIntent)
                                            } else {
                                                startService(svcIntent)
                                            }
                                        } catch (_: Exception) {}
                                        activeQueueManager.setQueue(
                                            items = activePlaylistTracks,
                                            startIndex = 0,
                                            isPlaylistContext = true,
                                            playlistId = effectivePlaylistId
                                        )
                                        navController.navigate(Screen.NowPlaying.route)
                                    },
                                    onPlayAllAsAudio = {
                                        try {
                                            val svcIntent = Intent(this@MainActivity, PlaybackService::class.java)
                                            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                                                ContextCompat.startForegroundService(this@MainActivity, svcIntent)
                                            } else {
                                                startService(svcIntent)
                                            }
                                        } catch (_: Exception) {}
                                        activeQueueManager.setPlaylistAudioMode(effectivePlaylistId, true)
                                        activeQueueManager.setQueue(
                                            items = activePlaylistTracks,
                                            startIndex = 0,
                                            isPlaylistContext = true,
                                            playlistId = effectivePlaylistId
                                        )
                                        navController.navigate(Screen.NowPlaying.route)
                                    },
                                    onShuffle = {
                                        val shuffled = activePlaylistTracks.shuffled()
                                        activeQueueManager.setQueue(
                                            items = shuffled,
                                            startIndex = 0,
                                            isPlaylistContext = true,
                                            playlistId = effectivePlaylistId
                                        )
                                        navController.navigate(Screen.NowPlaying.route)
                                    },
                                    onPlayTrack = { track ->
                                        try {
                                            val svcIntent = Intent(this@MainActivity, PlaybackService::class.java)
                                            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                                                ContextCompat.startForegroundService(this@MainActivity, svcIntent)
                                            } else {
                                                startService(svcIntent)
                                            }
                                        } catch (_: Exception) {}
                                        val trackIndex = activePlaylistTracks.indexOfFirst { it.id == track.id }
                                        if (trackIndex != -1) {
                                            activeQueueManager.setQueue(
                                                items = activePlaylistTracks,
                                                startIndex = trackIndex,
                                                isPlaylistContext = true,
                                                playlistId = effectivePlaylistId
                                            )
                                        } else {
                                            activeQueueManager.setSingleTrack(track)
                                        }
                                        navController.navigate(Screen.NowPlaying.route)
                                    },
                                    onPlaySelected = { selected ->
                                        try {
                                            val svcIntent = Intent(this@MainActivity, PlaybackService::class.java)
                                            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                                                ContextCompat.startForegroundService(this@MainActivity, svcIntent)
                                            } else {
                                                startService(svcIntent)
                                            }
                                        } catch (_: Exception) {}
                                        activeQueueManager.setQueue(
                                            items = selected,
                                            startIndex = 0,
                                            isPlaylistContext = true,
                                            playlistId = effectivePlaylistId
                                        )
                                        navController.navigate(Screen.NowPlaying.route)
                                    },
                                    onModeChange = { mode ->
                                        activeQueueManager.setPlaybackMode(mode)
                                    }
                                )
                            }

                            composable(Screen.Download.route) {
                                DownloadScreen(
                                    downloadRepository = downloadRepository,
                                    onPlayMedia = { item ->
                                        try {
                                            val svcIntent = Intent(this@MainActivity, PlaybackService::class.java)
                                            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                                                ContextCompat.startForegroundService(this@MainActivity, svcIntent)
                                            } else {
                                                startService(svcIntent)
                                            }
                                        } catch (_: Exception) {}
                                        activeQueueManager.setSingleTrack(item)
                                        navController.navigate(Screen.NowPlaying.route)
                                    },
                                    onMediaDownloaded = {
                                        coroutineScope.launch {
                                            val scanned = scanner.scanAllMedia()
                                            mediaList = scanned
                                            withContext(Dispatchers.IO) {
                                                db.mediaDao().insertAll(scanned.map { it.toEntity() })
                                            }
                                        }
                                    }
                                )
                            }

                            composable(Screen.ToolsAndTimer.route) {
                                if (activeSleepTimerManager != null) {
                                    SleepTimerScreen(
                                        sleepTimerManager = activeSleepTimerManager,
                                        onClose = { navController.popBackStack() }
                                    )
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private fun getMediaContentUri(context: Context, item: MediaItem): Uri? {
        if (item.uri.startsWith("content://")) {
            return Uri.parse(item.uri)
        }
        val collection = if (item.isVideo) {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) MediaStore.Video.Media.getContentUri(MediaStore.VOLUME_EXTERNAL)
            else MediaStore.Video.Media.EXTERNAL_CONTENT_URI
        } else {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) MediaStore.Audio.Media.getContentUri(MediaStore.VOLUME_EXTERNAL)
            else MediaStore.Audio.Media.EXTERNAL_CONTENT_URI
        }
        if (item.path.isNotBlank()) {
            try {
                val projection = arrayOf(MediaStore.MediaColumns._ID)
                val selection = "${MediaStore.MediaColumns.DATA} = ?"
                val selectionArgs = arrayOf(item.path)
                context.contentResolver.query(collection, projection, selection, selectionArgs, null)?.use { cursor ->
                    if (cursor.moveToFirst()) {
                        val id = cursor.getLong(cursor.getColumnIndexOrThrow(MediaStore.MediaColumns._ID))
                        return ContentUris.withAppendedId(collection, id)
                    }
                }
            } catch (_: Exception) {}
        }
        return if (item.id > 0) ContentUris.withAppendedId(collection, item.id) else null
    }

    private fun requestMediaPermissions() {
        val permissions = mutableListOf<String>()
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            permissions.add(Manifest.permission.READ_MEDIA_AUDIO)
            permissions.add(Manifest.permission.READ_MEDIA_VIDEO)
            permissions.add(Manifest.permission.POST_NOTIFICATIONS)
        } else {
            permissions.add(Manifest.permission.READ_EXTERNAL_STORAGE)
        }

        val ungranted = permissions.filter {
            ContextCompat.checkSelfPermission(this, it) != PackageManager.PERMISSION_GRANTED
        }

        if (ungranted.isNotEmpty()) {
            requestPermissionLauncher.launch(ungranted.toTypedArray())
        }
    }

    private fun bindPlaybackService() {
        val intent = Intent(this, PlaybackService::class.java)
        startService(intent)
        bindService(intent, serviceConnection, Context.BIND_AUTO_CREATE)
    }

    override fun onDestroy() {
        super.onDestroy()
        if (isBound) {
            unbindService(serviceConnection)
            isBound = false
        }
    }
}

