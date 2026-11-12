package com.mytube.novaplayer.ui.navigation

import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.FileDownload
import androidx.compose.material.icons.outlined.PlayCircle
import androidx.compose.material.icons.outlined.QueueMusic
import androidx.compose.material.icons.outlined.Tune
import androidx.compose.material.icons.outlined.VideoLibrary
import androidx.compose.ui.graphics.vector.ImageVector

sealed class Screen(val route: String, val title: String, val icon: ImageVector) {
    object MediaLibrary : Screen("media", "Media", Icons.Outlined.VideoLibrary)
    object Playlists : Screen("playlists", "Playlists", Icons.Outlined.QueueMusic)
    object NowPlaying : Screen("playing", "Playing", Icons.Outlined.PlayCircle)
    object Download : Screen("download", "Download", Icons.Outlined.FileDownload)
    object ToolsAndTimer : Screen("tools", "Tools", Icons.Outlined.Tune)
}
