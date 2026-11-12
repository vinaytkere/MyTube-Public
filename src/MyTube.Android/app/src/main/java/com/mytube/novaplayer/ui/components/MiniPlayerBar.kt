package com.mytube.novaplayer.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Pause
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.outlined.GraphicEq
import androidx.compose.material.icons.outlined.Headphones
import androidx.compose.material.icons.outlined.Movie
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
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
import com.mytube.novaplayer.data.model.MediaItem

@Composable
fun MiniPlayerBar(
    media: MediaItem,
    isPlaying: Boolean,
    isAudioOnlyMode: Boolean = false,
    onTogglePlayPause: () -> Unit,
    onStopAndClose: () -> Unit,
    onClickBar: () -> Unit
) {
    val model: Any = if (media.path.isNotBlank() && java.io.File(media.path).exists()) {
        java.io.File(media.path)
    } else {
        media.uri
    }

    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 12.dp, vertical = 4.dp)
            .shadow(elevation = 12.dp, shape = RoundedCornerShape(14.dp), spotColor = Color.Black)
            .clip(RoundedCornerShape(14.dp))
            .background(SurfaceContainerHigh)
            .clickable { onClickBar() }
            .padding(horizontal = 10.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(10.dp)
    ) {
        // Thumbnail with fallback and Audio Mode overlay
        Box(
            modifier = Modifier
                .size(42.dp)
                .clip(RoundedCornerShape(8.dp))
                .background(SurfaceContainerLowest),
            contentAlignment = Alignment.Center
        ) {
            AsyncImage(
                model = model,
                contentDescription = null,
                contentScale = ContentScale.Crop,
                modifier = Modifier.fillMaxSize()
            )

            if (isAudioOnlyMode && media.isVideo) {
                Box(
                    modifier = Modifier
                        .fillMaxSize()
                        .background(Color.Black.copy(alpha = 0.45f)),
                    contentAlignment = Alignment.Center
                ) {
                    Icon(
                        imageVector = Icons.Outlined.Headphones,
                        contentDescription = "Audio Mode",
                        tint = Primary,
                        modifier = Modifier.size(18.dp)
                    )
                }
            }
        }

        // Title and Subtitle
        Column(
            modifier = Modifier.weight(1f)
        ) {
            Text(
                text = media.title,
                style = Typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold, fontSize = 13.sp),
                color = OnSurface,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis
            )
            val modePrefix = if (media.isVideo && isAudioOnlyMode) "Audio Mode • " else ""
            Text(
                text = if (isPlaying) "${modePrefix}Playing • ${media.artistOrSubtitle}" else "${modePrefix}Paused • ${media.artistOrSubtitle}",
                style = Typography.labelSmall.copy(fontSize = 10.sp),
                color = if (isPlaying) Primary else OnSurfaceVariant,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis
            )
        }

        // Play / Pause Button
        IconButton(
            onClick = onTogglePlayPause,
            modifier = Modifier
                .size(36.dp)
                .clip(CircleShape)
                .background(PrimaryContainer)
        ) {
            Icon(
                imageVector = if (isPlaying) Icons.Default.Pause else Icons.Default.PlayArrow,
                contentDescription = "Play/Pause",
                tint = OnPrimaryContainer,
                modifier = Modifier.size(20.dp)
            )
        }

        // Close / Stop Button
        IconButton(
            onClick = onStopAndClose,
            modifier = Modifier
                .size(32.dp)
                .clip(CircleShape)
                .background(SurfaceContainer)
        ) {
            Icon(
                imageVector = Icons.Default.Close,
                contentDescription = "Stop & Close",
                tint = OnSurfaceVariant,
                modifier = Modifier.size(16.dp)
            )
        }
    }
}
