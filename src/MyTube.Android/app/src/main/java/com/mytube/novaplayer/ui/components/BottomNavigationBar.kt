package com.mytube.novaplayer.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.mytube.novaplayer.core.theme.*
import com.mytube.novaplayer.ui.navigation.Screen

@Composable
fun BottomNavigationBar(
    currentRoute: String,
    onNavigate: (String) -> Unit
) {
    val screens = listOf(
        Screen.MediaLibrary,
        Screen.Playlists,
        Screen.NowPlaying,
        Screen.Download,
        Screen.ToolsAndTimer
    )

    Box(
        modifier = Modifier
            .fillMaxWidth()
            .shadow(elevation = 24.dp, shape = RoundedCornerShape(topStart = 16.dp, topEnd = 16.dp), spotColor = Color.Black)
            .background(SurfaceContainerLowest.copy(alpha = 0.95f))
            .navigationBarsPadding()
            .height(64.dp)
    ) {
        Row(
            modifier = Modifier.fillMaxSize(),
            horizontalArrangement = Arrangement.SpaceAround,
            verticalAlignment = Alignment.CenterVertically
        ) {
            screens.forEach { screen ->
                val selected = currentRoute == screen.route
                val contentColor = if (selected) PrimaryContainer else OnSurfaceVariant

                Column(
                    modifier = Modifier
                        .clickable { onNavigate(screen.route) }
                        .padding(horizontal = 6.dp, vertical = 6.dp),
                    horizontalAlignment = Alignment.CenterHorizontally,
                    verticalArrangement = Arrangement.Center
                ) {
                    Icon(
                        imageVector = screen.icon,
                        contentDescription = screen.title,
                        tint = contentColor,
                        modifier = Modifier.size(24.dp)
                    )
                    Spacer(modifier = Modifier.height(2.dp))
                    Text(
                        text = screen.title,
                        color = contentColor,
                        style = Typography.labelSmall.copy(fontSize = 10.sp),
                        maxLines = 1
                    )
                }
            }
        }
    }
}
