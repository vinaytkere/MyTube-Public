package com.mytube.novaplayer.ui.screens.player

import android.app.Activity
import android.content.pm.ActivityInfo
import android.content.res.Configuration
import android.util.Log
import androidx.activity.compose.BackHandler
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.ui.platform.LocalConfiguration
import androidx.core.view.WindowCompat
import androidx.core.view.WindowInsetsCompat
import androidx.core.view.WindowInsetsControllerCompat
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material.icons.outlined.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.viewinterop.AndroidView
import com.mytube.novaplayer.core.theme.*
import com.mytube.novaplayer.data.model.MediaItem
import com.mytube.novaplayer.player.PlayerManager
import com.mytube.novaplayer.player.QueueManager
import kotlinx.coroutines.delay
import androidx.media3.ui.PlayerView
import androidx.media3.ui.AspectRatioFrameLayout

@Composable
fun PlayerDeckScreen(
    queueManager: QueueManager,
    playerManager: PlayerManager,
    onBack: () -> Unit,
    onStop: () -> Unit = { playerManager.stop(); onBack() },
    onOpenTools: () -> Unit,
    onSkipNext: () -> Unit = { queueManager.playNext() },
    onSkipPrevious: () -> Unit = { queueManager.playPrevious() }
) {
    val context = LocalContext.current
    val activity = context as? Activity

    val currentMedia by queueManager.currentMedia.collectAsState()
    val isPlaying by playerManager.isPlaying.collectAsState()
    val currentTimeMs by playerManager.currentTimeMs.collectAsState()
    val durationMs by playerManager.durationMs.collectAsState()
    val volume by playerManager.volume.collectAsState()
    val speed by playerManager.playbackSpeed.collectAsState()

    SideEffect {
        Log.i("NOVA_DEBUG", "PlayerDeckScreen recomposed: currentMedia='${currentMedia?.title}' (id=${currentMedia?.id}, isVideo=${currentMedia?.isVideo}), isPlaying=$isPlaying")
    }

    var isLocked by remember { mutableStateOf(false) }
    var abLoopState by remember { mutableStateOf(0) } // 0: Off, 1: Set A, 2: Loop A-B

    val configuration = LocalConfiguration.current
    val isDeviceLandscape = configuration.orientation == Configuration.ORIENTATION_LANDSCAPE
    val isVideo = currentMedia?.isVideo == true

    var isFullscreen by remember { mutableStateOf(false) }
    var showFullscreenControls by remember { mutableStateOf(true) }

    val isEffectiveFullscreen = (isVideo && isDeviceLandscape) || isFullscreen

    // Immersive Mode System Bars Controller
    val window = activity?.window
    DisposableEffect(isEffectiveFullscreen) {
        if (window != null) {
            try {
                val insetsController = WindowCompat.getInsetsController(window, window.decorView)
                if (isEffectiveFullscreen) {
                    insetsController.systemBarsBehavior = WindowInsetsControllerCompat.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE
                    insetsController.hide(WindowInsetsCompat.Type.systemBars())
                } else {
                    insetsController.show(WindowInsetsCompat.Type.systemBars())
                }
            } catch (e: Exception) {
                e.printStackTrace()
            }
        }
        onDispose {
            if (window != null) {
                try {
                    val insetsController = WindowCompat.getInsetsController(window, window.decorView)
                    insetsController.show(WindowInsetsCompat.Type.systemBars())
                } catch (_: Exception) {}
            }
        }
    }

    // Intercept back button in fullscreen mode
    BackHandler(enabled = isEffectiveFullscreen) {
        if (isFullscreen) {
            isFullscreen = false
            activity?.requestedOrientation = ActivityInfo.SCREEN_ORIENTATION_UNSPECIFIED
        } else {
            onBack()
        }
    }

    // Auto-restore orientation and detach views on dispose
    DisposableEffect(Unit) {
        onDispose {
            activity?.requestedOrientation = ActivityInfo.SCREEN_ORIENTATION_UNSPECIFIED
            playerManager.detachPlayerView()
        }
    }

    // Auto-hide controls in fullscreen after 3.5 seconds
    LaunchedEffect(showFullscreenControls, isEffectiveFullscreen) {
        if (isEffectiveFullscreen && showFullscreenControls) {
            delay(3500)
            showFullscreenControls = false
        }
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(if (isEffectiveFullscreen) Color.Black else Background)
    ) {
        // Top App Bar (only in portrait)
        if (!isEffectiveFullscreen) {
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .statusBarsPadding()
                    .padding(horizontal = 16.dp, vertical = 8.dp),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    IconButton(onClick = onBack) {
                        Icon(Icons.Default.ArrowBack, contentDescription = "Back", tint = OnSurface)
                    }
                    Box(
                        modifier = Modifier
                            .size(32.dp)
                            .clip(RoundedCornerShape(8.dp))
                            .background(PrimaryContainer),
                        contentAlignment = Alignment.Center
                    ) {
                        Icon(Icons.Default.PlayArrow, contentDescription = null, tint = OnPrimaryContainer, modifier = Modifier.size(18.dp))
                    }
                    Column {
                        Text(text = "Video Player Deck", style = Typography.headlineSmall, color = OnSurface)
                        Text(text = "NOVAPLAYER ENGINE", style = Typography.labelSmall, color = Primary)
                    }
                }

                Row(verticalAlignment = Alignment.CenterVertically) {
                    IconButton(onClick = {
                        isFullscreen = true
                        activity?.requestedOrientation = ActivityInfo.SCREEN_ORIENTATION_SENSOR_LANDSCAPE
                    }) {
                        Icon(Icons.Default.Fullscreen, contentDescription = "Fullscreen", tint = Primary)
                    }

                    IconButton(onClick = {}) {
                        Icon(Icons.Default.MoreVert, contentDescription = "Options", tint = OnSurfaceVariant)
                    }
                }
            }
        }

        // UNIFIED VIDEO VIEWPORT (SINGLE PERSISTENT AndroidView NODE)
        Box(
            modifier = if (isEffectiveFullscreen) {
                Modifier
                    .fillMaxSize()
                    .clickable(
                        interactionSource = remember { MutableInteractionSource() },
                        indication = null
                    ) {
                        showFullscreenControls = !showFullscreenControls
                    }
            } else {
                Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 16.dp, vertical = 6.dp)
                    .aspectRatio(16f / 9f)
                    .clip(RoundedCornerShape(16.dp))
                    .background(SurfaceContainerLowest)
            }
        ) {
            // PERSISTENT MEDIA3 PLAYERVIEW CANVAS
            AndroidView(
                factory = { ctx ->
                    PlayerView(ctx).apply {
                        useController = false
                        resizeMode = AspectRatioFrameLayout.RESIZE_MODE_FIT
                        playerManager.attachPlayerView(this)
                    }
                },
                update = { view ->
                    playerManager.attachPlayerView(view)
                },
                modifier = Modifier.fillMaxSize()
            )

            if (isEffectiveFullscreen) {
                // Fullscreen HUD Overlay
                androidx.compose.animation.AnimatedVisibility(
                    visible = showFullscreenControls,
                    enter = fadeIn(),
                    exit = fadeOut(),
                    modifier = Modifier.fillMaxSize()
                ) {
                    Box(
                        modifier = Modifier
                            .fillMaxSize()
                            .background(Color.Black.copy(alpha = 0.45f))
                    ) {
                        // Top Fullscreen Header Bar
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .align(Alignment.TopCenter)
                                .statusBarsPadding()
                                .padding(horizontal = 16.dp, vertical = 10.dp),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Row(
                                verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = Arrangement.spacedBy(8.dp),
                                modifier = Modifier.weight(1f)
                            ) {
                                IconButton(onClick = {
                                    if (isFullscreen) {
                                        isFullscreen = false
                                        activity?.requestedOrientation = ActivityInfo.SCREEN_ORIENTATION_UNSPECIFIED
                                    } else {
                                        onBack()
                                    }
                                }) {
                                    Icon(Icons.Default.ArrowBack, contentDescription = "Exit Fullscreen", tint = OnSurface)
                                }
                                Text(
                                    text = currentMedia?.title ?: "Video Playback",
                                    style = Typography.headlineSmall.copy(fontSize = 16.sp),
                                    color = OnSurface,
                                    maxLines = 1,
                                    overflow = TextOverflow.Ellipsis
                                )
                            }

                            Row(
                                horizontalArrangement = Arrangement.spacedBy(8.dp),
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                IconButton(
                                    onClick = {
                                        if (isDeviceLandscape) {
                                            activity?.requestedOrientation = ActivityInfo.SCREEN_ORIENTATION_PORTRAIT
                                        } else {
                                            activity?.requestedOrientation = ActivityInfo.SCREEN_ORIENTATION_SENSOR_LANDSCAPE
                                        }
                                    }
                                ) {
                                    Icon(
                                        imageVector = Icons.Default.ScreenRotation,
                                        contentDescription = "Rotate Screen",
                                        tint = Secondary
                                    )
                                }

                                if (isFullscreen) {
                                    IconButton(
                                        onClick = {
                                            isFullscreen = false
                                            activity?.requestedOrientation = ActivityInfo.SCREEN_ORIENTATION_UNSPECIFIED
                                        }
                                    ) {
                                        Icon(Icons.Default.FullscreenExit, contentDescription = "Exit Fullscreen", tint = Primary)
                                    }
                                }
                            }
                        }

                        // Center Transport Controls
                        Row(
                            modifier = Modifier.align(Alignment.Center),
                            horizontalArrangement = Arrangement.spacedBy(28.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            IconButton(
                                onClick = { playerManager.seekRelative(-10_000L) },
                                modifier = Modifier
                                    .size(50.dp)
                                    .clip(CircleShape)
                                    .background(SurfaceContainerHigh.copy(alpha = 0.8f))
                            ) {
                                Icon(Icons.Default.Replay10, contentDescription = "-10s", tint = OnSurface)
                            }

                            Box(
                                modifier = Modifier
                                    .size(72.dp)
                                    .shadow(elevation = 20.dp, shape = CircleShape, spotColor = PrimaryContainer)
                                    .clip(CircleShape)
                                    .background(PrimaryContainer)
                                    .clickable { playerManager.togglePlayPause() },
                                contentAlignment = Alignment.Center
                            ) {
                                Icon(
                                    imageVector = if (isPlaying) Icons.Default.Pause else Icons.Default.PlayArrow,
                                    contentDescription = "Play/Pause",
                                    tint = OnPrimaryContainer,
                                    modifier = Modifier.size(38.dp)
                                )
                            }

                            IconButton(
                                onClick = { playerManager.seekRelative(10_000L) },
                                modifier = Modifier
                                    .size(50.dp)
                                    .clip(CircleShape)
                                    .background(SurfaceContainerHigh.copy(alpha = 0.8f))
                            ) {
                                Icon(Icons.Default.Forward10, contentDescription = "+10s", tint = OnSurface)
                            }
                        }

                        // Bottom Fullscreen Controls Bar
                        Column(
                            modifier = Modifier
                                .fillMaxWidth()
                                .align(Alignment.BottomCenter)
                                .navigationBarsPadding()
                                .padding(horizontal = 20.dp, vertical = 14.dp)
                        ) {
                            PrecisionScrubberDeck(
                                currentTimeMs = currentTimeMs,
                                durationMs = if (durationMs > 0) durationMs else (currentMedia?.durationMs ?: 1L),
                                onSeek = { playerManager.seekTo(it) }
                            )

                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceBetween,
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Row(
                                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                                    verticalAlignment = Alignment.CenterVertically
                                ) {
                                    Button(
                                        onClick = onStop,
                                        colors = ButtonDefaults.buttonColors(
                                            containerColor = SurfaceContainerHigh,
                                            contentColor = Error
                                        ),
                                        shape = RoundedCornerShape(8.dp),
                                        contentPadding = PaddingValues(horizontal = 12.dp, vertical = 6.dp)
                                    ) {
                                        Icon(Icons.Default.Stop, contentDescription = null, modifier = Modifier.size(16.dp))
                                        Spacer(modifier = Modifier.width(4.dp))
                                        Text("Stop", style = Typography.labelSmall)
                                    }

                                    Text(
                                        text = if (isDeviceLandscape) "Landscape Mode" else "Portrait Mode",
                                        style = Typography.labelSmall,
                                        color = OnSurfaceVariant
                                    )
                                }

                                IconButton(
                                    onClick = {
                                        isFullscreen = false
                                        activity?.requestedOrientation = ActivityInfo.SCREEN_ORIENTATION_UNSPECIFIED
                                    }
                                ) {
                                    Icon(Icons.Default.FullscreenExit, contentDescription = "Exit Fullscreen", tint = Primary)
                                }
                            }
                        }
                    }
                }
            } else {
                // Portrait Video Canvas Top HUD
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .align(Alignment.TopCenter)
                        .background(
                            Brush.verticalGradient(
                                listOf(SurfaceContainerLowest.copy(alpha = 0.85f), Color.Transparent)
                            )
                        )
                        .padding(8.dp),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Box(
                        modifier = Modifier
                            .clip(RoundedCornerShape(4.dp))
                            .background(SurfaceContainerHigh.copy(alpha = 0.8f))
                            .padding(horizontal = 6.dp, vertical = 2.dp)
                    ) {
                        Text(text = "16:9", style = Typography.labelSmall, color = Secondary)
                    }

                    Box(
                        modifier = Modifier
                            .clip(RoundedCornerShape(4.dp))
                            .background(PrimaryContainer.copy(alpha = 0.85f))
                            .clickable {
                                isFullscreen = true
                                activity?.requestedOrientation = ActivityInfo.SCREEN_ORIENTATION_SENSOR_LANDSCAPE
                            }
                            .padding(horizontal = 6.dp, vertical = 2.dp)
                    ) {
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(4.dp)
                        ) {
                            Icon(Icons.Default.Fullscreen, contentDescription = "Fullscreen", tint = OnPrimaryContainer, modifier = Modifier.size(14.dp))
                            Text(text = "FULL", style = Typography.labelSmall, color = OnPrimaryContainer, fontWeight = FontWeight.Bold)
                        }
                    }
                }
            }
        }

        // Scrollable Portrait Controls (only in portrait)
        if (!isEffectiveFullscreen) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .weight(1f)
                    .verticalScroll(rememberScrollState())
                    .padding(bottom = 90.dp)
            ) {
                // Media Metadata & Tech Deck Hierarchy
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 16.dp, vertical = 8.dp)
                ) {
                    Text(
                        text = currentMedia?.title ?: "No Media Loaded",
                        style = Typography.headlineMedium,
                        color = OnSurface,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis
                    )
                    Text(
                        text = "Hardware Accelerated Media3 Engine",
                        style = Typography.bodySmall,
                        color = OnSurfaceVariant,
                        modifier = Modifier.padding(top = 2.dp)
                    )

                    Row(
                        horizontalArrangement = Arrangement.spacedBy(6.dp),
                        modifier = Modifier.padding(top = 8.dp)
                    ) {
                        CodecChip(text = "HW+ MEDIA3", bg = PrimaryContainer.copy(alpha = 0.20f), color = Primary)
                        CodecChip(text = "SURFACEVIEW", bg = SecondaryContainer.copy(alpha = 0.15f), color = Secondary)
                        CodecChip(text = "ZERO-COPY", bg = TertiaryContainer.copy(alpha = 0.20f), color = Tertiary)
                    }
                }

                // Audiophile / Precision Scrubber Deck
                PrecisionScrubberDeck(
                    currentTimeMs = currentTimeMs,
                    durationMs = if (durationMs > 0) durationMs else (currentMedia?.durationMs ?: 1L),
                    onSeek = { playerManager.seekTo(it) }
                )

                // Primary Transport Control Pod with STOP button
                PrimaryTransportPod(
                    isPlaying = isPlaying,
                    onTogglePlay = { playerManager.togglePlayPause() },
                    onStop = onStop,
                    onReplay10 = { playerManager.seekRelative(-10_000L) },
                    onForward10 = { playerManager.seekRelative(10_000L) },
                    onPrevious = {
                        if (currentTimeMs > 3000L) {
                            playerManager.seekTo(0L)
                        } else {
                            onSkipPrevious()
                        }
                    },
                    onNext = onSkipNext
                )

                // Secondary Quick Toolset Matrix
                QuickToolsetMatrix(
                    speed = speed,
                    onCycleSpeed = {
                        val nextSpeed = when (speed) {
                            1.0f -> 1.25f
                            1.25f -> 1.5f
                            1.5f -> 2.0f
                            2.0f -> 0.75f
                            else -> 1.0f
                        }
                        playerManager.setSpeed(nextSpeed)
                    },
                    abLoopState = abLoopState,
                    onCycleAbLoop = { abLoopState = (abLoopState + 1) % 3 },
                    isLocked = isLocked,
                    onToggleLock = { isLocked = !isLocked },
                    onOpenTools = onOpenTools
                )

                // Audio Frequency Spectrum Visualizer Card with Interactive Volume Slider
                AudioSpectrumCard(
                    volume = volume,
                    onVolumeChange = { playerManager.setVolume(it) }
                )
            }
        }
    }
}

@Composable
private fun CodecChip(text: String, bg: Color, color: Color) {
    Box(
        modifier = Modifier
            .clip(RoundedCornerShape(4.dp))
            .background(bg)
            .padding(horizontal = 7.dp, vertical = 3.dp)
    ) {
        Text(text = text, style = Typography.labelSmall, color = color)
    }
}

@Composable
private fun PrecisionScrubberDeck(
    currentTimeMs: Long,
    durationMs: Long,
    onSeek: (Long) -> Unit
) {
    val safeDuration = durationMs.coerceAtLeast(1L)
    var isDragging by remember { mutableStateOf(false) }
    var dragProgress by remember { mutableFloatStateOf(0f) }

    val actualProgress = (currentTimeMs.toFloat() / safeDuration.toFloat()).coerceIn(0f, 1f)
    val displayedProgress = if (isDragging) dragProgress else actualProgress
    val displayedTimeMs = if (isDragging) (dragProgress * safeDuration).toLong() else currentTimeMs

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp, vertical = 6.dp)
    ) {
        Slider(
            value = displayedProgress,
            onValueChange = {
                isDragging = true
                dragProgress = it
            },
            onValueChangeFinished = {
                val targetMs = (dragProgress * safeDuration).toLong().coerceIn(0L, safeDuration)
                onSeek(targetMs)
                isDragging = false
            },
            colors = SliderDefaults.colors(
                thumbColor = Primary,
                activeTrackColor = Primary,
                inactiveTrackColor = SurfaceContainerHighest
            ),
            modifier = Modifier.fillMaxWidth()
        )

        // Timecodes
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 4.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text(
                text = formatMs(displayedTimeMs),
                style = Typography.labelSmall,
                color = if (isDragging) Primary else OnSurface,
                fontWeight = FontWeight.Bold
            )
            Text(
                text = if (isDragging) "Scrubbing" else "Chapter: Main Sequence",
                style = Typography.labelSmall,
                color = if (isDragging) Primary else OnSurfaceVariant
            )
            Text(
                text = formatMs(safeDuration),
                style = Typography.labelSmall,
                color = OnSurfaceVariant
            )
        }
    }
}

@Composable
private fun PrimaryTransportPod(
    isPlaying: Boolean,
    onTogglePlay: () -> Unit,
    onStop: () -> Unit,
    onReplay10: () -> Unit,
    onForward10: () -> Unit,
    onPrevious: () -> Unit,
    onNext: () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp, vertical = 10.dp),
        horizontalArrangement = Arrangement.SpaceEvenly,
        verticalAlignment = Alignment.CenterVertically
    ) {
        IconButton(
            onClick = onReplay10,
            modifier = Modifier
                .size(44.dp)
                .clip(CircleShape)
                .background(SurfaceContainerLow)
        ) {
            Icon(Icons.Default.Replay10, contentDescription = "-10s", tint = OnSurfaceVariant)
        }

        IconButton(
            onClick = onPrevious,
            modifier = Modifier
                .size(44.dp)
                .clip(CircleShape)
                .background(SurfaceContainerLow)
        ) {
            Icon(Icons.Default.SkipPrevious, contentDescription = "Prev", tint = OnSurface)
        }

        // Central Glowing Neon Play / Pause Node
        Box(
            modifier = Modifier
                .size(64.dp)
                .shadow(elevation = 20.dp, shape = CircleShape, spotColor = PrimaryContainer)
                .clip(CircleShape)
                .background(PrimaryContainer)
                .clickable { onTogglePlay() },
            contentAlignment = Alignment.Center
        ) {
            Icon(
                imageVector = if (isPlaying) Icons.Default.Pause else Icons.Default.PlayArrow,
                contentDescription = "Play/Pause",
                tint = OnPrimaryContainer,
                modifier = Modifier.size(34.dp)
            )
        }

        // Dedicated Stop Button
        IconButton(
            onClick = onStop,
            modifier = Modifier
                .size(44.dp)
                .clip(CircleShape)
                .background(SurfaceContainerLow)
        ) {
            Icon(Icons.Default.Stop, contentDescription = "Stop", tint = Error)
        }

        IconButton(
            onClick = onNext,
            modifier = Modifier
                .size(44.dp)
                .clip(CircleShape)
                .background(SurfaceContainerLow)
        ) {
            Icon(Icons.Default.SkipNext, contentDescription = "Next", tint = OnSurface)
        }

        IconButton(
            onClick = onForward10,
            modifier = Modifier
                .size(44.dp)
                .clip(CircleShape)
                .background(SurfaceContainerLow)
        ) {
            Icon(Icons.Default.Forward10, contentDescription = "+10s", tint = OnSurfaceVariant)
        }
    }
}

@Composable
private fun QuickToolsetMatrix(
    speed: Float,
    onCycleSpeed: () -> Unit,
    abLoopState: Int,
    onCycleAbLoop: () -> Unit,
    isLocked: Boolean,
    onToggleLock: () -> Unit,
    onOpenTools: () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp, vertical = 10.dp)
            .clip(RoundedCornerShape(16.dp))
            .background(SurfaceContainerLow)
            .padding(8.dp),
        horizontalArrangement = Arrangement.SpaceAround,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            modifier = Modifier.clickable { onCycleSpeed() }
        ) {
            Icon(Icons.Outlined.Speed, contentDescription = null, tint = Secondary, modifier = Modifier.size(20.dp))
            Text(text = "${speed}x", style = Typography.labelSmall, color = OnSurface, fontWeight = FontWeight.Bold)
        }

        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            modifier = Modifier.clickable { onCycleAbLoop() }
        ) {
            Icon(Icons.Outlined.Repeat, contentDescription = null, tint = if (abLoopState > 0) Primary else OnSurfaceVariant, modifier = Modifier.size(20.dp))
            val label = when (abLoopState) {
                1 -> "Set [A]"
                2 -> "Loop [A-B]"
                else -> "A-B Off"
            }
            Text(text = label, style = Typography.labelSmall, color = if (abLoopState > 0) Primary else OnSurfaceVariant)
        }

        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            modifier = Modifier.clickable { }
        ) {
            Icon(Icons.Outlined.Equalizer, contentDescription = null, tint = Tertiary, modifier = Modifier.size(20.dp))
            Text(text = "DSP 10-Band", style = Typography.labelSmall, color = Tertiary)
        }

        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            modifier = Modifier.clickable { onOpenTools() }
        ) {
            Icon(Icons.Outlined.Bedtime, contentDescription = null, tint = OnSurfaceVariant, modifier = Modifier.size(20.dp))
            Text(text = "Timer", style = Typography.labelSmall, color = OnSurfaceVariant)
        }

        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            modifier = Modifier.clickable { onToggleLock() }
        ) {
            Icon(
                imageVector = if (isLocked) Icons.Default.Lock else Icons.Default.LockOpen,
                contentDescription = null,
                tint = if (isLocked) PrimaryContainer else OnSurfaceVariant,
                modifier = Modifier.size(20.dp)
            )
            Text(
                text = if (isLocked) "Locked" else "Unlocked",
                style = Typography.labelSmall,
                color = if (isLocked) PrimaryContainer else OnSurfaceVariant
            )
        }
    }
}

@Composable
private fun AudioSpectrumCard(
    volume: Int,
    onVolumeChange: (Int) -> Unit
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp, vertical = 8.dp)
            .clip(RoundedCornerShape(16.dp))
            .background(SurfaceContainer)
            .padding(14.dp)
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                Box(
                    modifier = Modifier
                        .size(8.dp)
                        .clip(CircleShape)
                        .background(TertiaryContainer)
                )
                Text(text = "Audio Matrix & Output", style = Typography.headlineSmall, color = OnSurface)
            }
            Box(
                modifier = Modifier
                    .clip(RoundedCornerShape(4.dp))
                    .background(PrimaryContainer.copy(alpha = 0.4f))
                    .padding(horizontal = 6.dp, vertical = 2.dp)
            ) {
                Text(text = "$volume%", style = Typography.labelSmall, color = Primary, fontWeight = FontWeight.Bold)
            }
        }

        // Interactive Audio Output Volume Slider
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 8.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            IconButton(
                onClick = {
                    if (volume > 0) onVolumeChange(0) else onVolumeChange(100)
                },
                modifier = Modifier.size(28.dp)
            ) {
                Icon(
                    imageVector = if (volume == 0) Icons.Default.VolumeOff else if (volume < 50) Icons.Default.VolumeDown else Icons.Default.VolumeUp,
                    contentDescription = "Mute/Unmute",
                    tint = if (volume == 0) Error else Primary,
                    modifier = Modifier.size(20.dp)
                )
            }
            Slider(
                value = volume.toFloat(),
                onValueChange = { onVolumeChange(it.toInt()) },
                valueRange = 0f..100f,
                colors = SliderDefaults.colors(
                    thumbColor = Primary,
                    activeTrackColor = Primary,
                    inactiveTrackColor = SurfaceContainerHighest
                ),
                modifier = Modifier.weight(1f)
            )
        }

        // Animated Spectrum Bars (Mint -> Cyan -> Amber)
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .height(32.dp)
                .padding(top = 6.dp),
            horizontalArrangement = Arrangement.spacedBy(4.dp),
            verticalAlignment = Alignment.Bottom
        ) {
            val heights = listOf(0.4f, 0.65f, 0.9f, 0.75f, 1.0f, 0.82f, 0.6f, 0.45f, 0.35f, 0.5f, 0.7f, 0.4f, 0.2f)
            heights.forEachIndexed { idx, h ->
                val barColor = when {
                    idx < 5 -> TertiaryContainer
                    idx < 8 -> SecondaryContainer
                    else -> PrimaryContainer
                }
                Box(
                    modifier = Modifier
                        .weight(1f)
                        .fillMaxHeight(h)
                        .clip(RoundedCornerShape(topStart = 2.dp, topEnd = 2.dp))
                        .background(barColor)
                )
            }
        }

        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 6.dp),
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            Text(text = "Channel: FL / FR / C / LFE / SL / SR", style = Typography.labelSmall, color = OnSurfaceVariant)
            Text(text = "Sample: 48.0 kHz • 640 kbps", style = Typography.labelSmall, color = Secondary)
        }
    }
}

private fun formatMs(ms: Long): String {
    val totalSeconds = ms / 1000
    val minutes = totalSeconds / 60
    val seconds = totalSeconds % 60
    return String.format("%02d:%02d", minutes, seconds)
}
