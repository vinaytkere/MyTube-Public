package com.mytube.novaplayer.ui.screens.playlists

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material.icons.outlined.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import coil.compose.AsyncImage
import com.mytube.novaplayer.core.theme.*
import com.mytube.novaplayer.data.local.entity.PlaylistEntity
import com.mytube.novaplayer.data.model.MediaItem
import com.mytube.novaplayer.player.PlaybackMode

@Composable
fun PlaylistScreen(
    playlists: List<PlaylistEntity>,
    selectedPlaylistId: Long?,
    onSelectPlaylist: (Long) -> Unit,
    tracks: List<MediaItem>,
    allLibraryMedia: List<MediaItem>,
    onCreatePlaylist: (String) -> Unit,
    onRenamePlaylist: (Long, String) -> Unit,
    onDeletePlaylist: (Long) -> Unit,
    onAddTracksToPlaylist: (Long, List<MediaItem>) -> Unit,
    onRemoveTrackFromPlaylist: (Long, Long) -> Unit,
    onReorderTracks: (Long, List<MediaItem>) -> Unit,
    onPlayAll: () -> Unit,
    onPlayAllAsAudio: () -> Unit = onPlayAll,
    isPlaylistAudioMode: Boolean = false,
    onTogglePlaylistAudioMode: (Boolean) -> Unit = {},
    onShuffle: () -> Unit,
    onPlayTrack: (MediaItem) -> Unit,
    onPlaySelected: (List<MediaItem>) -> Unit,
    onModeChange: (PlaybackMode) -> Unit
) {
    val selectedItemIds = remember { mutableStateListOf<Long>() }
    var activeMode by remember { mutableStateOf(PlaybackMode.PLAY_SELECTED) }

    // Dialog state
    var showCreateDialog by remember { mutableStateOf(false) }
    var createNameInput by remember { mutableStateOf("") }

    var showRenameDialog by remember { mutableStateOf(false) }
    var renameNameInput by remember { mutableStateOf("") }

    var showDeleteConfirmDialog by remember { mutableStateOf(false) }

    var showAddMediaDialog by remember { mutableStateOf(false) }
    val mediaToAddIds = remember { mutableStateListOf<Long>() }

    val activePlaylist = playlists.firstOrNull { it.id == selectedPlaylistId }
        ?: playlists.firstOrNull()

    // Auto-select first playlist if none selected
    LaunchedEffect(playlists, selectedPlaylistId) {
        if (selectedPlaylistId == null && playlists.isNotEmpty()) {
            onSelectPlaylist(playlists.first().id)
        }
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(Background)
            .statusBarsPadding()
    ) {
        LazyColumn(
            modifier = Modifier
                .fillMaxSize()
                .padding(horizontal = 16.dp),
            contentPadding = PaddingValues(top = 10.dp, bottom = 100.dp),
            verticalArrangement = Arrangement.spacedBy(14.dp)
        ) {
            // Horizontal Playlist Selector Strip (+ New Playlist and all existing playlists)
            item {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .horizontalScroll(rememberScrollState()),
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    // + New Playlist Button
                    Button(
                        onClick = {
                            createNameInput = ""
                            showCreateDialog = true
                        },
                        colors = ButtonDefaults.buttonColors(
                            containerColor = PrimaryContainer,
                            contentColor = OnPrimaryContainer
                        ),
                        shape = RoundedCornerShape(12.dp),
                        contentPadding = PaddingValues(horizontal = 12.dp, vertical = 8.dp)
                    ) {
                        Icon(Icons.Default.Add, contentDescription = null, modifier = Modifier.size(16.dp))
                        Spacer(modifier = Modifier.width(4.dp))
                        Text(text = "New Playlist", style = Typography.labelMedium, fontWeight = FontWeight.Bold)
                    }

                    // Existing Playlists Chips
                    playlists.forEach { pl ->
                        val isSelected = pl.id == activePlaylist?.id
                        val chipBg = if (isSelected) SurfaceContainerHighest else SurfaceContainer
                        val chipText = if (isSelected) Primary else OnSurface

                        Row(
                            modifier = Modifier
                                .clip(RoundedCornerShape(12.dp))
                                .background(chipBg)
                                .border(
                                    width = if (isSelected) 1.5.dp else 0.dp,
                                    color = if (isSelected) Primary else Color.Transparent,
                                    shape = RoundedCornerShape(12.dp)
                                )
                                .clickable { onSelectPlaylist(pl.id) }
                                .padding(horizontal = 14.dp, vertical = 9.dp),
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(6.dp)
                        ) {
                            Icon(
                                imageVector = Icons.Default.QueueMusic,
                                contentDescription = null,
                                tint = chipText,
                                modifier = Modifier.size(16.dp)
                            )
                            Text(
                                text = pl.name,
                                style = Typography.labelMedium,
                                color = chipText,
                                fontWeight = if (isSelected) FontWeight.Bold else FontWeight.Normal
                            )
                        }
                    }
                }
            }

            if (activePlaylist == null) {
                item {
                    // No playlist created yet
                    Column(
                        modifier = Modifier
                            .fillMaxWidth()
                            .clip(RoundedCornerShape(16.dp))
                            .background(SurfaceContainerLow)
                            .padding(24.dp),
                        horizontalAlignment = Alignment.CenterHorizontally,
                        verticalArrangement = Arrangement.spacedBy(10.dp)
                    ) {
                        Icon(Icons.Default.QueueMusic, contentDescription = null, tint = Primary, modifier = Modifier.size(44.dp))
                        Text(text = "No Playlists Yet", style = Typography.headlineMedium, color = OnSurface)
                        Text(text = "Create your first playlist to organize your favorite audio and video tracks.", style = Typography.bodySmall, color = OnSurfaceVariant)
                        Button(
                            onClick = {
                                createNameInput = ""
                                showCreateDialog = true
                            },
                            colors = ButtonDefaults.buttonColors(containerColor = PrimaryContainer, contentColor = OnPrimaryContainer)
                        ) {
                            Text("Create Playlist")
                        }
                    }
                }
            } else {
                item {
                    // Dynamic Atmospheric Header Card for Selected Playlist
                    Column(
                        modifier = Modifier
                            .fillMaxWidth()
                            .clip(RoundedCornerShape(16.dp))
                            .background(SurfaceContainerLow)
                            .padding(16.dp)
                    ) {
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Row(
                                verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = Arrangement.spacedBy(6.dp),
                                modifier = Modifier
                                    .clip(CircleShape)
                                    .background(PrimaryContainer.copy(alpha = 0.2f))
                                    .padding(horizontal = 8.dp, vertical = 3.dp)
                            ) {
                                Box(
                                    modifier = Modifier
                                        .size(6.dp)
                                        .clip(CircleShape)
                                        .background(PrimaryContainer)
                                )
                                Text(text = "PLAYLIST", style = Typography.labelSmall, color = Primary)
                            }

                            Row(horizontalArrangement = Arrangement.spacedBy(4.dp)) {
                                // Rename Button
                                IconButton(
                                    onClick = {
                                        renameNameInput = activePlaylist.name
                                        showRenameDialog = true
                                    },
                                    modifier = Modifier.size(32.dp)
                                ) {
                                    Icon(Icons.Default.Edit, contentDescription = "Rename", tint = Secondary, modifier = Modifier.size(18.dp))
                                }

                                // Delete Button
                                IconButton(
                                    onClick = { showDeleteConfirmDialog = true },
                                    modifier = Modifier.size(32.dp)
                                ) {
                                    Icon(Icons.Default.Delete, contentDescription = "Delete", tint = Error, modifier = Modifier.size(18.dp))
                                }
                            }
                        }

                        Text(
                            text = activePlaylist.name,
                            style = Typography.headlineLarge,
                            color = OnSurface,
                            modifier = Modifier.padding(top = 8.dp)
                        )

                        val totalDurationMs = tracks.sumOf { it.durationMs }
                        val totalMinutes = totalDurationMs / 60000

                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(6.dp),
                            modifier = Modifier.padding(top = 4.dp)
                        ) {
                            Text(text = "${tracks.size} items", style = Typography.labelMedium, color = OnSurface, fontWeight = FontWeight.Bold)
                            Text(text = "•", style = Typography.labelMedium, color = OnSurfaceVariant)
                            Text(text = "${totalMinutes}m runtime", style = Typography.labelMedium, color = OnSurfaceVariant)
                            if (selectedItemIds.isNotEmpty()) {
                                Text(text = "•", style = Typography.labelMedium, color = OnSurfaceVariant)
                                Text(text = "${selectedItemIds.size} SELECTED", style = Typography.labelSmall, color = Secondary)
                            }
                        }

                        // Action Strip: Add Media button
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(top = 14.dp),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Button(
                                onClick = {
                                    mediaToAddIds.clear()
                                    showAddMediaDialog = true
                                },
                                colors = ButtonDefaults.buttonColors(containerColor = SurfaceContainerHigh, contentColor = Secondary),
                                shape = RoundedCornerShape(10.dp)
                            ) {
                                Icon(Icons.Outlined.AddCircle, contentDescription = null, modifier = Modifier.size(18.dp))
                                Spacer(modifier = Modifier.width(6.dp))
                                Text(text = "Add Media", style = Typography.labelMedium)
                            }

                            if (tracks.isNotEmpty()) {
                                Text(
                                    text = "Use ▲ ▼ to reorder tracks",
                                    style = Typography.labelSmall,
                                    color = OnSurfaceVariant
                                )
                            }
                        }
                    }
                }

                // Audio Mode for Videos Switch (shown if playlist has any video files)
                if (tracks.any { it.isVideo }) {
                    item {
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .clip(RoundedCornerShape(14.dp))
                                .background(if (isPlaylistAudioMode) Tertiary.copy(alpha = 0.12f) else SurfaceContainerLow)
                                .border(
                                    width = 1.dp,
                                    color = if (isPlaylistAudioMode) Tertiary.copy(alpha = 0.5f) else Color.Transparent,
                                    shape = RoundedCornerShape(14.dp)
                                )
                                .clickable { onTogglePlaylistAudioMode(!isPlaylistAudioMode) }
                                .padding(horizontal = 14.dp, vertical = 10.dp),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Row(
                                verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = Arrangement.spacedBy(12.dp),
                                modifier = Modifier.weight(1f)
                            ) {
                                Box(
                                    modifier = Modifier
                                        .size(38.dp)
                                        .clip(CircleShape)
                                        .background(if (isPlaylistAudioMode) Tertiary else SurfaceContainerHigh),
                                    contentAlignment = Alignment.Center
                                ) {
                                    Icon(
                                        imageVector = Icons.Outlined.Headphones,
                                        contentDescription = null,
                                        tint = if (isPlaylistAudioMode) OnTertiary else OnSurfaceVariant,
                                        modifier = Modifier.size(20.dp)
                                    )
                                }
                                Column {
                                    Text(
                                        text = "Audio Mode for Videos",
                                        style = Typography.labelLarge,
                                        color = OnSurface,
                                        fontWeight = FontWeight.Bold
                                    )
                                    Text(
                                        text = if (isPlaylistAudioMode)
                                            "ON: Videos in this playlist play as audio (screen-off)"
                                        else
                                            "OFF: Videos in this playlist play in video mode",
                                        style = Typography.bodySmall,
                                        color = if (isPlaylistAudioMode) Tertiary else OnSurfaceVariant
                                    )
                                }
                            }
                            Switch(
                                checked = isPlaylistAudioMode,
                                onCheckedChange = { onTogglePlaylistAudioMode(it) },
                                colors = SwitchDefaults.colors(
                                    checkedThumbColor = OnTertiary,
                                    checkedTrackColor = Tertiary,
                                    uncheckedThumbColor = OnSurfaceVariant,
                                    uncheckedTrackColor = SurfaceContainerHighest
                                )
                            )
                        }
                    }
                }

                item {
                    // Main Playlist Controls Bar
                    Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.spacedBy(10.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            // Play All Button (Neon Amber Glow)
                            Button(
                                onClick = onPlayAll,
                                enabled = tracks.isNotEmpty(),
                                colors = ButtonDefaults.buttonColors(containerColor = PrimaryContainer, contentColor = OnPrimaryContainer),
                                shape = RoundedCornerShape(14.dp),
                                modifier = Modifier
                                    .weight(1f)
                                    .height(52.dp)
                                    .shadow(elevation = 16.dp, shape = RoundedCornerShape(14.dp), spotColor = PrimaryContainer)
                            ) {
                                Icon(Icons.Default.PlayArrow, contentDescription = null, modifier = Modifier.size(24.dp))
                                Spacer(modifier = Modifier.width(6.dp))
                                Text(text = "Play All", style = Typography.headlineSmall, fontWeight = FontWeight.Bold)
                            }

                            // Play All as Audio Button (Screen Off)
                            IconButton(
                                onClick = onPlayAllAsAudio,
                                enabled = tracks.isNotEmpty(),
                                modifier = Modifier
                                    .size(52.dp)
                                    .clip(RoundedCornerShape(14.dp))
                                    .background(SurfaceContainerHigh)
                            ) {
                                Icon(Icons.Outlined.Headphones, contentDescription = "Play as Audio (Screen Off)", tint = Tertiary, modifier = Modifier.size(24.dp))
                            }

                            // Shuffle Button
                            IconButton(
                                onClick = onShuffle,
                                enabled = tracks.isNotEmpty(),
                                modifier = Modifier
                                    .size(52.dp)
                                    .clip(RoundedCornerShape(14.dp))
                                    .background(SurfaceContainerHigh)
                            ) {
                                Icon(Icons.Default.Shuffle, contentDescription = "Shuffle", tint = Secondary, modifier = Modifier.size(24.dp))
                            }
                        }

                        // Play Selected Button
                        if (selectedItemIds.isNotEmpty()) {
                            Button(
                                onClick = {
                                    val selected = tracks.filter { selectedItemIds.contains(it.id) }
                                    onPlaySelected(selected)
                                },
                                colors = ButtonDefaults.buttonColors(containerColor = SurfaceContainerHigh, contentColor = Secondary),
                                shape = RoundedCornerShape(10.dp),
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .height(44.dp)
                            ) {
                                Icon(Icons.Default.PlaylistPlay, contentDescription = null, modifier = Modifier.size(18.dp))
                                Spacer(modifier = Modifier.width(6.dp))
                                Text(text = "Play Selected (${selectedItemIds.size} tracks)", style = Typography.labelLarge)
                            }
                        }
                    }
                }

                item {
                    // Execution Rule Chips
                    Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                        Text(text = "EXECUTION RULE", style = Typography.labelSmall, color = OnSurfaceVariant)
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .horizontalScroll(rememberScrollState()),
                            horizontalArrangement = Arrangement.spacedBy(8.dp)
                        ) {
                            ModeChip(
                                title = "Play 1 Only",
                                isSelected = activeMode == PlaybackMode.PLAY_SINGLE,
                                onClick = {
                                    activeMode = PlaybackMode.PLAY_SINGLE
                                    onModeChange(PlaybackMode.PLAY_SINGLE)
                                }
                            )
                            ModeChip(
                                title = "Play Selected (${selectedItemIds.size})",
                                isSelected = activeMode == PlaybackMode.PLAY_SELECTED,
                                onClick = {
                                    activeMode = PlaybackMode.PLAY_SELECTED
                                    onModeChange(PlaybackMode.PLAY_SELECTED)
                                }
                            )
                            ModeChip(
                                title = "Loop Playlist",
                                isSelected = activeMode == PlaybackMode.LOOP_PLAYLIST,
                                onClick = {
                                    activeMode = PlaybackMode.LOOP_PLAYLIST
                                    onModeChange(PlaybackMode.LOOP_PLAYLIST)
                                }
                            )
                            ModeChip(
                                title = "Shuffle All",
                                isSelected = activeMode == PlaybackMode.SHUFFLE,
                                onClick = {
                                    activeMode = PlaybackMode.SHUFFLE
                                    onModeChange(PlaybackMode.SHUFFLE)
                                }
                            )
                        }
                    }
                }

                item {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(text = "MEDIA SEQUENCE (${tracks.size})", style = Typography.labelSmall, color = OnSurfaceVariant)
                        Text(text = "Click ▲ / ▼ to reorder", style = Typography.labelSmall, color = Primary)
                    }
                }

                if (tracks.isEmpty()) {
                    item {
                        Box(
                            modifier = Modifier
                                .fillMaxWidth()
                                .clip(RoundedCornerShape(12.dp))
                                .background(SurfaceContainerLow)
                                .padding(24.dp),
                            contentAlignment = Alignment.Center
                        ) {
                            Text(
                                text = "Playlist is empty. Tap 'Add Media' above to add songs and videos.",
                                style = Typography.bodyMedium,
                                color = OnSurfaceVariant
                            )
                        }
                    }
                }

                // Track Items with WORKING REORDER (Up/Down Buttons)
                itemsIndexed(tracks, key = { _, item -> item.id }) { index, track ->
                    val isSelected = selectedItemIds.contains(track.id)

                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .clip(RoundedCornerShape(12.dp))
                            .background(if (isSelected) SurfaceContainerHigh else SurfaceContainer)
                            .clickable { onPlayTrack(track) }
                            .padding(horizontal = 8.dp, vertical = 8.dp),
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        // Checkbox
                        Box(
                            modifier = Modifier
                                .size(22.dp)
                                .clip(RoundedCornerShape(6.dp))
                                .background(if (isSelected) PrimaryContainer else SurfaceContainerLow)
                                .clickable {
                                    if (isSelected) selectedItemIds.remove(track.id)
                                    else selectedItemIds.add(track.id)
                                },
                            contentAlignment = Alignment.Center
                        ) {
                            if (isSelected) {
                                Icon(Icons.Default.Check, contentDescription = null, tint = OnPrimaryContainer, modifier = Modifier.size(15.dp))
                            }
                        }

                        // Thumbnail with duration pill
                        Box(
                            modifier = Modifier
                                .width(56.dp)
                                .height(40.dp)
                                .clip(RoundedCornerShape(6.dp))
                                .background(SurfaceContainerLowest)
                                .clickable { onPlayTrack(track) },
                            contentAlignment = Alignment.Center
                        ) {
                            val model: Any = if (track.path.isNotBlank() && java.io.File(track.path).exists()) {
                                java.io.File(track.path)
                            } else {
                                track.uri
                            }
                            AsyncImage(
                                model = model,
                                contentDescription = null,
                                contentScale = ContentScale.Crop,
                                modifier = Modifier.fillMaxSize()
                            )
                            Box(
                                modifier = Modifier
                                    .fillMaxSize()
                                    .background(Color.Black.copy(alpha = 0.25f)),
                                contentAlignment = Alignment.Center
                            ) {
                                Icon(
                                    imageVector = if (track.isVideo) Icons.Outlined.Movie else Icons.Default.PlayArrow,
                                    contentDescription = null,
                                    tint = if (track.isVideo) Secondary else OnSurface,
                                    modifier = Modifier.size(16.dp)
                                )
                            }
                        }

                        // Title & Metadata
                        Column(modifier = Modifier.weight(1f)) {
                            Text(
                                text = "${String.format("%02d", index + 1)}. ${track.title}",
                                style = Typography.bodyMedium,
                                fontWeight = FontWeight.SemiBold,
                                color = OnSurface,
                                maxLines = 1,
                                overflow = TextOverflow.Ellipsis
                            )
                            Row(
                                verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = Arrangement.spacedBy(6.dp),
                                modifier = Modifier.padding(top = 2.dp)
                            ) {
                                val badge = track.codecBadges.firstOrNull() ?: if (track.isVideo) "VIDEO" else "AUDIO"
                                Box(
                                    modifier = Modifier
                                        .clip(RoundedCornerShape(3.dp))
                                        .background(if (track.isVideo) SecondaryContainer.copy(alpha = 0.2f) else TertiaryContainer.copy(alpha = 0.2f))
                                        .padding(horizontal = 4.dp, vertical = 1.dp)
                                ) {
                                    Text(
                                        text = badge,
                                        style = Typography.labelSmall.copy(fontSize = 9.sp),
                                        color = if (track.isVideo) Secondary else Tertiary
                                    )
                                }
                                Text(text = track.artistOrSubtitle, style = Typography.labelSmall, color = OnSurfaceVariant, maxLines = 1, overflow = TextOverflow.Ellipsis)
                                Text(text = "• ${track.formattedDuration}", style = Typography.labelSmall, color = OnSurfaceVariant)
                            }
                        }

                        // WORKING REORDER CONTROLS: UP & DOWN ARROWS
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(2.dp)
                        ) {
                            // Move Up
                            IconButton(
                                onClick = {
                                    if (index > 0) {
                                        val mutableList = tracks.toMutableList()
                                        val item = mutableList.removeAt(index)
                                        mutableList.add(index - 1, item)
                                        onReorderTracks(activePlaylist.id, mutableList)
                                    }
                                },
                                enabled = index > 0,
                                modifier = Modifier.size(30.dp)
                            ) {
                                Icon(
                                    imageVector = Icons.Default.KeyboardArrowUp,
                                    contentDescription = "Move Up",
                                    tint = if (index > 0) Primary else OnSurfaceVariant.copy(alpha = 0.3f),
                                    modifier = Modifier.size(20.dp)
                                )
                            }

                            // Move Down
                            IconButton(
                                onClick = {
                                    if (index < tracks.size - 1) {
                                        val mutableList = tracks.toMutableList()
                                        val item = mutableList.removeAt(index)
                                        mutableList.add(index + 1, item)
                                        onReorderTracks(activePlaylist.id, mutableList)
                                    }
                                },
                                enabled = index < tracks.size - 1,
                                modifier = Modifier.size(30.dp)
                            ) {
                                Icon(
                                    imageVector = Icons.Default.KeyboardArrowDown,
                                    contentDescription = "Move Down",
                                    tint = if (index < tracks.size - 1) Primary else OnSurfaceVariant.copy(alpha = 0.3f),
                                    modifier = Modifier.size(20.dp)
                                )
                            }

                            // Remove from playlist
                            IconButton(
                                onClick = {
                                    onRemoveTrackFromPlaylist(activePlaylist.id, track.id)
                                    selectedItemIds.remove(track.id)
                                },
                                modifier = Modifier.size(30.dp)
                            ) {
                                Icon(
                                    imageVector = Icons.Default.Close,
                                    contentDescription = "Remove",
                                    tint = OnSurfaceVariant,
                                    modifier = Modifier.size(16.dp)
                                )
                            }
                        }
                    }
                }
            }
        }
    }

    // DIALOG: CREATE NEW PLAYLIST
    if (showCreateDialog) {
        AlertDialog(
            onDismissRequest = { showCreateDialog = false },
            title = { Text("Create New Playlist", style = Typography.headlineSmall, color = OnSurface) },
            text = {
                OutlinedTextField(
                    value = createNameInput,
                    onValueChange = { createNameInput = it },
                    label = { Text("Playlist Name") },
                    placeholder = { Text("e.g. Chill Beats") },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth(),
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedBorderColor = Primary,
                        focusedLabelColor = Primary,
                        cursorColor = Primary
                    )
                )
            },
            confirmButton = {
                Button(
                    onClick = {
                        if (createNameInput.isNotBlank()) {
                            onCreatePlaylist(createNameInput.trim())
                            showCreateDialog = false
                        }
                    },
                    enabled = createNameInput.isNotBlank(),
                    colors = ButtonDefaults.buttonColors(containerColor = PrimaryContainer, contentColor = OnPrimaryContainer)
                ) {
                    Text("Create")
                }
            },
            dismissButton = {
                TextButton(onClick = { showCreateDialog = false }) {
                    Text("Cancel", color = OnSurfaceVariant)
                }
            },
            containerColor = SurfaceContainerHigh,
            shape = RoundedCornerShape(16.dp)
        )
    }

    // DIALOG: RENAME PLAYLIST
    if (showRenameDialog && activePlaylist != null) {
        AlertDialog(
            onDismissRequest = { showRenameDialog = false },
            title = { Text("Rename Playlist", style = Typography.headlineSmall, color = OnSurface) },
            text = {
                OutlinedTextField(
                    value = renameNameInput,
                    onValueChange = { renameNameInput = it },
                    label = { Text("New Playlist Name") },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth(),
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedBorderColor = Primary,
                        focusedLabelColor = Primary,
                        cursorColor = Primary
                    )
                )
            },
            confirmButton = {
                Button(
                    onClick = {
                        if (renameNameInput.isNotBlank()) {
                            onRenamePlaylist(activePlaylist.id, renameNameInput.trim())
                            showRenameDialog = false
                        }
                    },
                    enabled = renameNameInput.isNotBlank(),
                    colors = ButtonDefaults.buttonColors(containerColor = PrimaryContainer, contentColor = OnPrimaryContainer)
                ) {
                    Text("Save")
                }
            },
            dismissButton = {
                TextButton(onClick = { showRenameDialog = false }) {
                    Text("Cancel", color = OnSurfaceVariant)
                }
            },
            containerColor = SurfaceContainerHigh,
            shape = RoundedCornerShape(16.dp)
        )
    }

    // DIALOG: DELETE PLAYLIST CONFIRMATION
    if (showDeleteConfirmDialog && activePlaylist != null) {
        AlertDialog(
            onDismissRequest = { showDeleteConfirmDialog = false },
            title = { Text("Delete Playlist", style = Typography.headlineSmall, color = Error) },
            text = {
                Text(
                    text = "Are you sure you want to delete the playlist '${activePlaylist.name}'? Tracks will not be deleted from your device.",
                    style = Typography.bodyMedium,
                    color = OnSurface
                )
            },
            confirmButton = {
                Button(
                    onClick = {
                        onDeletePlaylist(activePlaylist.id)
                        showDeleteConfirmDialog = false
                    },
                    colors = ButtonDefaults.buttonColors(containerColor = Error, contentColor = OnError)
                ) {
                    Text("Delete")
                }
            },
            dismissButton = {
                TextButton(onClick = { showDeleteConfirmDialog = false }) {
                    Text("Cancel", color = OnSurfaceVariant)
                }
            },
            containerColor = SurfaceContainerHigh,
            shape = RoundedCornerShape(16.dp)
        )
    }

    // DIALOG: ADD MEDIA TO PLAYLIST
    if (showAddMediaDialog && activePlaylist != null) {
        var searchQuery by remember { mutableStateOf("") }
        val availableMedia = remember(allLibraryMedia, searchQuery) {
            if (searchQuery.isBlank()) allLibraryMedia
            else allLibraryMedia.filter { it.title.contains(searchQuery, ignoreCase = true) }
        }

        AlertDialog(
            onDismissRequest = { showAddMediaDialog = false },
            title = { Text("Add Media to '${activePlaylist.name}'", style = Typography.headlineSmall, color = OnSurface) },
            text = {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(380.dp),
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    OutlinedTextField(
                        value = searchQuery,
                        onValueChange = { searchQuery = it },
                        placeholder = { Text("Search songs / videos...") },
                        singleLine = true,
                        leadingIcon = { Icon(Icons.Default.Search, contentDescription = null, tint = OnSurfaceVariant) },
                        modifier = Modifier.fillMaxWidth(),
                        colors = OutlinedTextFieldDefaults.colors(
                            focusedBorderColor = Primary,
                            cursorColor = Primary
                        )
                    )

                    Text(
                        text = "${mediaToAddIds.size} selected of ${availableMedia.size}",
                        style = Typography.labelSmall,
                        color = Secondary
                    )

                    LazyColumn(
                        modifier = Modifier
                            .weight(1f)
                            .fillMaxWidth(),
                        verticalArrangement = Arrangement.spacedBy(6.dp)
                    ) {
                        items(availableMedia, key = { it.id }) { item ->
                            val isChecked = mediaToAddIds.contains(item.id)
                            Row(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .clip(RoundedCornerShape(8.dp))
                                    .background(if (isChecked) SurfaceContainerHighest else SurfaceContainer)
                                    .clickable {
                                        if (isChecked) mediaToAddIds.remove(item.id)
                                        else mediaToAddIds.add(item.id)
                                    }
                                    .padding(horizontal = 10.dp, vertical = 8.dp),
                                verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = Arrangement.spacedBy(10.dp)
                            ) {
                                Checkbox(
                                    checked = isChecked,
                                    onCheckedChange = { checked ->
                                        if (checked) mediaToAddIds.add(item.id)
                                        else mediaToAddIds.remove(item.id)
                                    },
                                    colors = CheckboxDefaults.colors(checkedColor = Primary)
                                )

                                Icon(
                                    imageVector = if (item.isVideo) Icons.Outlined.Movie else Icons.Outlined.GraphicEq,
                                    contentDescription = null,
                                    tint = if (item.isVideo) Secondary else Tertiary,
                                    modifier = Modifier.size(18.dp)
                                )

                                Column(modifier = Modifier.weight(1f)) {
                                    Text(
                                        text = item.title,
                                        style = Typography.bodySmall,
                                        color = OnSurface,
                                        maxLines = 1,
                                        overflow = TextOverflow.Ellipsis
                                    )
                                    Text(
                                        text = "${item.artistOrSubtitle} • ${item.formattedDuration}",
                                        style = Typography.labelSmall.copy(fontSize = 10.sp),
                                        color = OnSurfaceVariant
                                    )
                                }
                            }
                        }
                    }
                }
            },
            confirmButton = {
                Button(
                    onClick = {
                        val selectedItems = allLibraryMedia.filter { mediaToAddIds.contains(it.id) }
                        if (selectedItems.isNotEmpty()) {
                            onAddTracksToPlaylist(activePlaylist.id, selectedItems)
                            showAddMediaDialog = false
                        }
                    },
                    enabled = mediaToAddIds.isNotEmpty(),
                    colors = ButtonDefaults.buttonColors(containerColor = PrimaryContainer, contentColor = OnPrimaryContainer)
                ) {
                    Text("Add (${mediaToAddIds.size})")
                }
            },
            dismissButton = {
                TextButton(onClick = { showAddMediaDialog = false }) {
                    Text("Cancel", color = OnSurfaceVariant)
                }
            },
            containerColor = SurfaceContainerHigh,
            shape = RoundedCornerShape(16.dp)
        )
    }
}

@Composable
private fun ModeChip(
    title: String,
    isSelected: Boolean,
    onClick: () -> Unit
) {
    val bg = if (isSelected) PrimaryContainer.copy(alpha = 0.25f) else SurfaceContainer
    val textColor = if (isSelected) Primary else OnSurfaceVariant

    Box(
        modifier = Modifier
            .clip(CircleShape)
            .background(bg)
            .clickable { onClick() }
            .padding(horizontal = 12.dp, vertical = 7.dp)
    ) {
        Text(text = title, style = Typography.labelSmall, color = textColor, fontWeight = if (isSelected) FontWeight.Bold else FontWeight.Normal)
    }
}
