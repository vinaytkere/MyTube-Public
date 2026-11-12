package com.mytube.novaplayer.ui.screens.library

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
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
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.mytube.novaplayer.core.theme.*
import com.mytube.novaplayer.data.local.entity.PlaylistEntity
import com.mytube.novaplayer.data.model.MediaItem

enum class MediaFilter {
    ALL, VIDEOS, AUDIO, FOLDERS
}

@Composable
fun MediaLibraryScreen(
    mediaList: List<MediaItem>,
    selectedFilter: MediaFilter,
    onSelectFilter: (MediaFilter) -> Unit,
    currentlyPlayingMediaId: Long?,
    isPlaying: Boolean,
    playlists: List<PlaylistEntity>,
    onPlayTrack: (MediaItem) -> Unit,
    onTogglePlayPause: () -> Unit,
    onPlaySelected: (List<MediaItem>) -> Unit,
    onRescan: () -> Unit,
    onDeleteItems: (List<MediaItem>) -> Unit,
    onAddToPlaylist: (Long, List<MediaItem>) -> Unit,
    onCreatePlaylistAndAdd: (String, List<MediaItem>) -> Unit
) {
    val selectedItemIds = remember { mutableStateListOf<Long>() }

    // Dialog state for deletion confirmation
    var itemsToDelete by remember { mutableStateOf<List<MediaItem>?>(null) }

    // Dialog state for add to playlist
    var itemsToAddToPlaylist by remember { mutableStateOf<List<MediaItem>?>(null) }
    var showCreatePlaylistInDialog by remember { mutableStateOf(false) }
    var newPlaylistNameInput by remember { mutableStateOf("") }

    // Filter media items
    val filteredList = remember(mediaList, selectedFilter) {
        when (selectedFilter) {
            MediaFilter.ALL -> mediaList
            MediaFilter.VIDEOS -> mediaList.filter { it.isVideo }
            MediaFilter.AUDIO -> mediaList.filter { !it.isVideo }
            MediaFilter.FOLDERS -> mediaList
        }
    }

    val selectedCount = selectedItemIds.size
    val totalSelectedBytes = remember(selectedItemIds.toList(), mediaList) {
        mediaList.filter { selectedItemIds.contains(it.id) }.sumOf { it.sizeBytes }
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(Background)
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .statusBarsPadding()
        ) {
            // Header Top Bar
            HeaderTopBar()

            LazyColumn(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(horizontal = 16.dp),
                contentPadding = PaddingValues(bottom = 120.dp),
                verticalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                item {
                    // Storage Path & Rescan Bar
                    StoragePathBar(onRescan = onRescan)
                }

                item {
                    // Filter Pills Horizontal Reel
                    FilterPillsRow(
                        selectedFilter = selectedFilter,
                        totalCount = mediaList.size,
                        videoCount = mediaList.count { it.isVideo },
                        audioCount = mediaList.count { !it.isVideo },
                        onSelectFilter = onSelectFilter
                    )
                }

                if (selectedCount > 0) {
                    item {
                        // Multi-selection State Banner
                        MultiSelectBanner(
                            selectedCount = selectedCount,
                            totalBytes = totalSelectedBytes,
                            onDeselectAll = { selectedItemIds.clear() }
                        )
                    }
                }

                // Media Items
                items(filteredList, key = { it.id }) { item ->
                    val isSelected = selectedItemIds.contains(item.id)
                    val isCurrentlyPlaying = currentlyPlayingMediaId == item.id

                    MediaCardItem(
                        item = item,
                        isSelected = isSelected,
                        isCurrentlyPlaying = isCurrentlyPlaying,
                        isPlaying = isPlaying,
                        onToggleSelect = {
                            if (isSelected) selectedItemIds.remove(item.id)
                            else selectedItemIds.add(item.id)
                        },
                        onPlayClick = {
                            onPlayTrack(item)
                        },
                        onAddToPlaylist = {
                            itemsToAddToPlaylist = listOf(item)
                        },
                        onDeleteClick = {
                            itemsToDelete = listOf(item)
                        }
                    )
                }

                item {
                    // Storage Metrics Footnote
                    StorageMetricsFooter()
                }
            }
        }

        // Floating Contextual Toolbar
        AnimatedVisibility(
            visible = selectedCount > 0,
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .padding(bottom = 76.dp, start = 16.dp, end = 16.dp)
        ) {
            FloatingSelectionToolbar(
                selectedCount = selectedCount,
                onPlay = {
                    val selectedItems = mediaList.filter { selectedItemIds.contains(it.id) }
                    onPlaySelected(selectedItems)
                },
                onAddToPlaylist = {
                    val selectedItems = mediaList.filter { selectedItemIds.contains(it.id) }
                    itemsToAddToPlaylist = selectedItems
                },
                onDelete = {
                    val selectedItems = mediaList.filter { selectedItemIds.contains(it.id) }
                    itemsToDelete = selectedItems
                }
            )
        }
    }

    // Confirmation Dialog for Deleting Files
    if (itemsToDelete != null) {
        val targets = itemsToDelete ?: emptyList()
        val count = targets.size
        val message = if (count == 1) {
            "Are you sure you want to delete '${targets.first().title}' permanently from your device? This cannot be undone."
        } else {
            "Are you sure you want to permanently delete $count selected files from your device? This cannot be undone."
        }

        AlertDialog(
            onDismissRequest = { itemsToDelete = null },
            title = {
                Text(
                    text = "Delete File${if (count > 1) "s" else ""}",
                    style = Typography.headlineSmall,
                    color = Error
                )
            },
            text = {
                Text(
                    text = message,
                    style = Typography.bodyMedium,
                    color = OnSurface
                )
            },
            confirmButton = {
                Button(
                    onClick = {
                        onDeleteItems(targets)
                        selectedItemIds.removeAll(targets.map { it.id }.toSet())
                        itemsToDelete = null
                    },
                    colors = ButtonDefaults.buttonColors(containerColor = Error, contentColor = OnError)
                ) {
                    Text("Delete Permanently")
                }
            },
            dismissButton = {
                TextButton(onClick = { itemsToDelete = null }) {
                    Text("Cancel", color = OnSurfaceVariant)
                }
            },
            containerColor = SurfaceContainerHigh,
            shape = RoundedCornerShape(16.dp)
        )
    }

    // Dialog for Adding to Playlist
    if (itemsToAddToPlaylist != null) {
        val itemsToAdd = itemsToAddToPlaylist ?: emptyList()

        AlertDialog(
            onDismissRequest = {
                itemsToAddToPlaylist = null
                showCreatePlaylistInDialog = false
                newPlaylistNameInput = ""
            },
            title = {
                Text(
                    text = "Add to Playlist (${itemsToAdd.size})",
                    style = Typography.headlineSmall,
                    color = OnSurface
                )
            },
            text = {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(top = 4.dp),
                    verticalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    if (showCreatePlaylistInDialog) {
                        OutlinedTextField(
                            value = newPlaylistNameInput,
                            onValueChange = { newPlaylistNameInput = it },
                            label = { Text("Playlist Name") },
                            singleLine = true,
                            modifier = Modifier.fillMaxWidth(),
                            colors = OutlinedTextFieldDefaults.colors(
                                focusedBorderColor = Primary,
                                focusedLabelColor = Primary,
                                cursorColor = Primary
                            )
                        )
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.End,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            TextButton(onClick = { showCreatePlaylistInDialog = false }) {
                                Text("Back", color = OnSurfaceVariant)
                            }
                            Button(
                                onClick = {
                                    if (newPlaylistNameInput.isNotBlank()) {
                                        onCreatePlaylistAndAdd(newPlaylistNameInput.trim(), itemsToAdd)
                                        itemsToAddToPlaylist = null
                                        showCreatePlaylistInDialog = false
                                        newPlaylistNameInput = ""
                                    }
                                },
                                enabled = newPlaylistNameInput.isNotBlank(),
                                colors = ButtonDefaults.buttonColors(containerColor = PrimaryContainer, contentColor = OnPrimaryContainer)
                            ) {
                                Text("Create & Add")
                            }
                        }
                    } else {
                        // Button to create new playlist
                        Button(
                            onClick = { showCreatePlaylistInDialog = true },
                            colors = ButtonDefaults.buttonColors(containerColor = SurfaceContainerHighest, contentColor = Secondary),
                            shape = RoundedCornerShape(10.dp),
                            modifier = Modifier.fillMaxWidth()
                        ) {
                            Icon(Icons.Default.Add, contentDescription = null, modifier = Modifier.size(18.dp))
                            Spacer(modifier = Modifier.width(6.dp))
                            Text("+ New Playlist")
                        }

                        if (playlists.isEmpty()) {
                            Text(
                                text = "No playlists found. Create a new one above!",
                                style = Typography.bodySmall,
                                color = OnSurfaceVariant,
                                modifier = Modifier.padding(vertical = 8.dp)
                            )
                        } else {
                            Text(
                                text = "Choose existing playlist:",
                                style = Typography.labelSmall,
                                color = OnSurfaceVariant
                            )

                            Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                                playlists.forEach { pl ->
                                    Row(
                                        modifier = Modifier
                                            .fillMaxWidth()
                                            .clip(RoundedCornerShape(8.dp))
                                            .background(SurfaceContainer)
                                            .clickable {
                                                onAddToPlaylist(pl.id, itemsToAdd)
                                                itemsToAddToPlaylist = null
                                            }
                                            .padding(horizontal = 12.dp, vertical = 10.dp),
                                        verticalAlignment = Alignment.CenterVertically,
                                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                                    ) {
                                        Icon(Icons.Default.QueueMusic, contentDescription = null, tint = Primary, modifier = Modifier.size(20.dp))
                                        Text(text = pl.name, style = Typography.bodyMedium, color = OnSurface, fontWeight = FontWeight.SemiBold)
                                    }
                                }
                            }
                        }
                    }
                }
            },
            confirmButton = {},
            dismissButton = {
                TextButton(onClick = {
                    itemsToAddToPlaylist = null
                    showCreatePlaylistInDialog = false
                    newPlaylistNameInput = ""
                }) {
                    Text("Cancel", color = OnSurfaceVariant)
                }
            },
            containerColor = SurfaceContainerHigh,
            shape = RoundedCornerShape(16.dp)
        )
    }
}

@Composable
private fun HeaderTopBar() {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp, vertical = 12.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Row(
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            // Neon Brand Icon
            Box(
                modifier = Modifier
                    .size(34.dp)
                    .clip(RoundedCornerShape(8.dp))
                    .background(PrimaryContainer),
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    imageVector = Icons.Default.PlayArrow,
                    contentDescription = null,
                    tint = OnPrimaryContainer,
                    modifier = Modifier.size(20.dp)
                )
            }

            Column {
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(6.dp)
                ) {
                    Text(
                        text = "NovaPlayer",
                        style = Typography.headlineSmall,
                        color = OnSurface
                    )
                    Box(
                        modifier = Modifier
                            .clip(RoundedCornerShape(4.dp))
                            .background(TertiaryContainer.copy(alpha = 0.2f))
                            .padding(horizontal = 6.dp, vertical = 2.dp)
                    ) {
                        Text(
                            text = "MEDIA3",
                            style = Typography.labelSmall,
                            color = Tertiary
                        )
                    }
                }
                Text(
                    text = "MEDIA LIBRARY",
                    style = Typography.labelSmall,
                    color = Primary
                )
            }
        }

        Row(
            horizontalArrangement = Arrangement.spacedBy(4.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            IconButton(onClick = {}) {
                Icon(Icons.Outlined.Search, contentDescription = "Search", tint = OnSurfaceVariant)
            }
            IconButton(onClick = {}) {
                Icon(Icons.Outlined.Cast, contentDescription = "Cast", tint = OnSurfaceVariant)
            }
            IconButton(onClick = {}) {
                Icon(Icons.Outlined.Equalizer, contentDescription = "Equalizer", tint = OnSurfaceVariant)
            }
            Box(
                modifier = Modifier
                    .size(32.dp)
                    .clip(CircleShape)
                    .background(Primary),
                contentAlignment = Alignment.Center
            ) {
                Icon(Icons.Default.Person, contentDescription = "Profile", tint = OnPrimary, modifier = Modifier.size(18.dp))
            }
        }
    }
}

@Composable
private fun StoragePathBar(onRescan: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(SurfaceContainerLow)
            .padding(10.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Row(
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(6.dp),
            modifier = Modifier.weight(1f)
        ) {
            Icon(Icons.Default.FolderOpen, contentDescription = null, tint = Primary, modifier = Modifier.size(18.dp))
            Text(
                text = "/storage/emulated/0/Media",
                style = Typography.labelSmall,
                color = OnSurfaceVariant,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis
            )
        }

        Row(
            modifier = Modifier
                .clip(CircleShape)
                .background(SurfaceContainer)
                .clickable { onRescan() }
                .padding(horizontal = 10.dp, vertical = 4.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(4.dp)
        ) {
            Icon(Icons.Default.Sync, contentDescription = null, tint = Secondary, modifier = Modifier.size(14.dp))
            Text(text = "RESCAN", style = Typography.labelSmall, color = Secondary)
        }
    }
}

@Composable
private fun FilterPillsRow(
    selectedFilter: MediaFilter,
    totalCount: Int,
    videoCount: Int,
    audioCount: Int,
    onSelectFilter: (MediaFilter) -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .horizontalScroll(rememberScrollState()),
        horizontalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        FilterPill(
            title = "All",
            badge = totalCount.toString(),
            isSelected = selectedFilter == MediaFilter.ALL,
            onClick = { onSelectFilter(MediaFilter.ALL) }
        )
        FilterPill(
            title = "Videos",
            badge = videoCount.toString(),
            icon = Icons.Outlined.Movie,
            iconTint = Secondary,
            isSelected = selectedFilter == MediaFilter.VIDEOS,
            onClick = { onSelectFilter(MediaFilter.VIDEOS) }
        )
        FilterPill(
            title = "Audio",
            badge = audioCount.toString(),
            icon = Icons.Outlined.GraphicEq,
            iconTint = Tertiary,
            isSelected = selectedFilter == MediaFilter.AUDIO,
            onClick = { onSelectFilter(MediaFilter.AUDIO) }
        )
        FilterPill(
            title = "Folders",
            badge = null,
            icon = Icons.Outlined.Folder,
            isSelected = selectedFilter == MediaFilter.FOLDERS,
            onClick = { onSelectFilter(MediaFilter.FOLDERS) }
        )
    }
}

@Composable
private fun FilterPill(
    title: String,
    badge: String?,
    icon: androidx.compose.ui.graphics.vector.ImageVector? = null,
    iconTint: Color = OnSurfaceVariant,
    isSelected: Boolean,
    onClick: () -> Unit
) {
    val bg = if (isSelected) PrimaryContainer else SurfaceContainer
    val textColor = if (isSelected) OnPrimaryContainer else OnSurfaceVariant

    Row(
        modifier = Modifier
            .clip(CircleShape)
            .background(bg)
            .clickable { onClick() }
            .padding(horizontal = 12.dp, vertical = 7.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(6.dp)
    ) {
        if (icon != null) {
            Icon(icon, contentDescription = null, tint = if (isSelected) OnPrimaryContainer else iconTint, modifier = Modifier.size(15.dp))
        }
        Text(text = title, style = Typography.labelMedium, color = textColor, fontWeight = FontWeight.SemiBold)
        if (badge != null) {
            Text(text = badge, style = Typography.labelSmall, color = textColor.copy(alpha = 0.75f))
        }
    }
}

@Composable
private fun MultiSelectBanner(
    selectedCount: Int,
    totalBytes: Long,
    onDeselectAll: () -> Unit
) {
    val formattedGb = String.format("%.2f GB staged", totalBytes / (1024.0 * 1024.0 * 1024.0))

    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(SurfaceContainerHigh)
            .padding(horizontal = 14.dp, vertical = 10.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Row(
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Box(
                modifier = Modifier
                    .size(26.dp)
                    .clip(RoundedCornerShape(8.dp))
                    .background(PrimaryContainer),
                contentAlignment = Alignment.Center
            ) {
                Icon(Icons.Default.DoneAll, contentDescription = null, tint = OnPrimaryContainer, modifier = Modifier.size(16.dp))
            }
            Column {
                Text(text = "$selectedCount items selected", style = Typography.headlineSmall, color = OnSurface)
                Text(text = formattedGb, style = Typography.labelSmall, color = Primary)
            }
        }

        Row(verticalAlignment = Alignment.CenterVertically) {
            TextButton(onClick = onDeselectAll) {
                Text(text = "DESELECT", style = Typography.labelSmall, color = OnSurface)
            }
            IconButton(onClick = onDeselectAll) {
                Icon(Icons.Default.Close, contentDescription = "Close", tint = OnSurfaceVariant, modifier = Modifier.size(18.dp))
            }
        }
    }
}

@Composable
private fun MediaCardItem(
    item: MediaItem,
    isSelected: Boolean,
    isCurrentlyPlaying: Boolean,
    isPlaying: Boolean,
    onToggleSelect: () -> Unit,
    onPlayClick: () -> Unit,
    onAddToPlaylist: () -> Unit,
    onDeleteClick: () -> Unit
) {
    val cardBg = if (isSelected) SurfaceContainer else SurfaceContainerLow
    var menuExpanded by remember { mutableStateOf(false) }

    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(cardBg)
            .border(
                width = if (isCurrentlyPlaying) 1.5.dp else 0.dp,
                color = if (isCurrentlyPlaying) Primary else Color.Transparent,
                shape = RoundedCornerShape(12.dp)
            )
            .clickable { onPlayClick() }
            .padding(10.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(10.dp)
    ) {
        // Neon Amber Indicator Strip for selected item
        if (isSelected) {
            Box(
                modifier = Modifier
                    .width(4.dp)
                    .height(44.dp)
                    .clip(CircleShape)
                    .background(PrimaryContainer)
            )
        }

        // Checkbox box
        Box(
            modifier = Modifier
                .size(24.dp)
                .clip(RoundedCornerShape(6.dp))
                .background(if (isSelected) PrimaryContainer else SurfaceContainerHigh)
                .clickable { onToggleSelect() },
            contentAlignment = Alignment.Center
        ) {
            if (isSelected) {
                Icon(Icons.Default.Check, contentDescription = null, tint = OnPrimaryContainer, modifier = Modifier.size(16.dp))
            }
        }

        // Thumbnail representation with duration pill
        Box(
            modifier = Modifier
                .width(80.dp)
                .height(56.dp)
                .clip(RoundedCornerShape(8.dp))
                .background(SurfaceContainerLowest)
                .clickable { onPlayClick() },
            contentAlignment = Alignment.Center
        ) {
            if (isCurrentlyPlaying) {
                Icon(
                    imageVector = if (isPlaying) Icons.Default.Pause else Icons.Default.PlayArrow,
                    contentDescription = null,
                    tint = Primary,
                    modifier = Modifier.size(28.dp)
                )
            } else {
                Icon(
                    imageVector = if (item.isVideo) Icons.Outlined.Movie else Icons.Outlined.GraphicEq,
                    contentDescription = null,
                    tint = if (item.isVideo) Secondary else Tertiary,
                    modifier = Modifier.size(24.dp)
                )
            }

            Box(
                modifier = Modifier
                    .align(Alignment.BottomEnd)
                    .padding(4.dp)
                    .clip(RoundedCornerShape(4.dp))
                    .background(SurfaceContainerLowest.copy(alpha = 0.9f))
                    .padding(horizontal = 4.dp, vertical = 1.dp)
            ) {
                Text(text = item.formattedDuration, style = Typography.labelSmall, color = OnSurface)
            }
        }

        // Metadata
        Column(
            modifier = Modifier.weight(1f)
        ) {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(6.dp)
            ) {
                Text(
                    text = item.title,
                    style = Typography.headlineSmall.copy(fontSize = 15.sp),
                    color = if (isCurrentlyPlaying) Primary else OnSurface,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                    modifier = Modifier.weight(1f, fill = false)
                )
                if (isCurrentlyPlaying) {
                    Box(
                        modifier = Modifier
                            .clip(RoundedCornerShape(4.dp))
                            .background(PrimaryContainer)
                            .padding(horizontal = 4.dp, vertical = 1.dp)
                    ) {
                        Text(
                            text = if (isPlaying) "PLAYING" else "PAUSED",
                            style = Typography.labelSmall.copy(fontSize = 8.sp),
                            color = OnPrimaryContainer,
                            fontWeight = FontWeight.Bold
                        )
                    }
                }
            }

            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(6.dp),
                modifier = Modifier.padding(top = 2.dp)
            ) {
                Text(text = item.formattedSize, style = Typography.labelSmall, color = OnSurfaceVariant)
                Text(text = "•", style = Typography.labelSmall, color = OnSurfaceVariant)
                Text(
                    text = item.artistOrSubtitle,
                    style = Typography.labelSmall,
                    color = if (item.isVideo) OnSurfaceVariant else Primary
                )
            }

            // Codec Badges
            Row(
                horizontalArrangement = Arrangement.spacedBy(4.dp),
                modifier = Modifier.padding(top = 4.dp)
            ) {
                item.codecBadges.take(3).forEach { badge ->
                    val badgeBg = if (badge.contains("4K") || badge.contains("1080")) SecondaryContainer.copy(alpha = 0.2f)
                    else if (badge.contains("FLAC") || badge.contains("WAV")) TertiaryContainer.copy(alpha = 0.2f)
                    else SurfaceContainerHigh

                    val badgeColor = if (badge.contains("4K") || badge.contains("1080")) Secondary
                    else if (badge.contains("FLAC") || badge.contains("WAV")) Tertiary
                    else OnSurfaceVariant

                    Box(
                        modifier = Modifier
                            .clip(RoundedCornerShape(4.dp))
                            .background(badgeBg)
                            .padding(horizontal = 6.dp, vertical = 2.dp)
                    ) {
                        Text(text = badge, style = Typography.labelSmall, color = badgeColor)
                    }
                }
            }
        }

        // 3-dots Menu Button
        Box {
            IconButton(onClick = { menuExpanded = true }) {
                Icon(Icons.Default.MoreVert, contentDescription = "More", tint = OnSurfaceVariant)
            }

            DropdownMenu(
                expanded = menuExpanded,
                onDismissRequest = { menuExpanded = false },
                modifier = Modifier.background(SurfaceContainerHigh)
            ) {
                DropdownMenuItem(
                    text = { Text("Play Now", color = OnSurface) },
                    leadingIcon = { Icon(Icons.Default.PlayArrow, contentDescription = null, tint = Primary) },
                    onClick = {
                        menuExpanded = false
                        onPlayClick()
                    }
                )
                DropdownMenuItem(
                    text = { Text("Add to Playlist", color = OnSurface) },
                    leadingIcon = { Icon(Icons.Default.PlaylistAdd, contentDescription = null, tint = Secondary) },
                    onClick = {
                        menuExpanded = false
                        onAddToPlaylist()
                    }
                )
                HorizontalDivider(color = SurfaceContainerHighest)
                DropdownMenuItem(
                    text = { Text("Delete File", color = Error) },
                    leadingIcon = { Icon(Icons.Default.Delete, contentDescription = null, tint = Error) },
                    onClick = {
                        menuExpanded = false
                        onDeleteClick()
                    }
                )
            }
        }
    }
}

@Composable
private fun StorageMetricsFooter() {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(SurfaceContainerLowest)
            .padding(12.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Row(
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(6.dp)
        ) {
            Icon(Icons.Outlined.Equalizer, contentDescription = null, tint = Tertiary, modifier = Modifier.size(16.dp))
            Text(text = "Internal Storage • Available", style = Typography.labelSmall, color = OnSurfaceVariant)
        }
        Text(text = "Indexed just now", style = Typography.labelSmall, color = Primary)
    }
}

@Composable
private fun FloatingSelectionToolbar(
    selectedCount: Int,
    onPlay: () -> Unit,
    onAddToPlaylist: () -> Unit,
    onDelete: () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .shadow(elevation = 16.dp, shape = RoundedCornerShape(18.dp), spotColor = Color.Black)
            .clip(RoundedCornerShape(18.dp))
            .background(SurfaceContainerHigh.copy(alpha = 0.95f))
            .padding(8.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        // Play Selected Neon Button
        Button(
            onClick = onPlay,
            colors = ButtonDefaults.buttonColors(containerColor = PrimaryContainer, contentColor = OnPrimaryContainer),
            shape = RoundedCornerShape(12.dp),
            contentPadding = PaddingValues(horizontal = 16.dp, vertical = 10.dp)
        ) {
            Icon(Icons.Default.PlayArrow, contentDescription = null, modifier = Modifier.size(18.dp))
            Spacer(modifier = Modifier.width(6.dp))
            Text(text = "Play ($selectedCount)", style = Typography.headlineSmall.copy(fontSize = 15.sp))
        }

        Row(horizontalArrangement = Arrangement.spacedBy(4.dp)) {
            IconButton(onClick = onAddToPlaylist) {
                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    Icon(Icons.Default.PlaylistAdd, contentDescription = "Add", tint = Secondary, modifier = Modifier.size(20.dp))
                    Text(text = "PLAYLIST", style = Typography.labelSmall.copy(fontSize = 9.sp), color = OnSurfaceVariant)
                }
            }
            IconButton(onClick = onDelete) {
                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    Icon(Icons.Default.Delete, contentDescription = "Delete", tint = Error, modifier = Modifier.size(20.dp))
                    Text(text = "DELETE", style = Typography.labelSmall.copy(fontSize = 9.sp), color = Error)
                }
            }
        }
    }
}
