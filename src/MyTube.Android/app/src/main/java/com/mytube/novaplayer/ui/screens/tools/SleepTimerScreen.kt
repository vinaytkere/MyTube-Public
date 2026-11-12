package com.mytube.novaplayer.ui.screens.tools

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
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
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.mytube.novaplayer.core.theme.*
import com.mytube.novaplayer.player.SleepTimerManager

@Composable
fun SleepTimerScreen(
    sleepTimerManager: SleepTimerManager,
    onClose: () -> Unit
) {
    val isActive by sleepTimerManager.isActive.collectAsState()
    val remainingMs by sleepTimerManager.remainingTimeMs.collectAsState()
    val totalMs by sleepTimerManager.totalDurationMs.collectAsState()

    var selectedMinutes by remember { mutableStateOf(30) }
    var volumeFadeOut by remember { mutableStateOf(true) }
    var pauseInsteadOfStop by remember { mutableStateOf(true) }
    var sleepDevice by remember { mutableStateOf(false) }
    var waitForTrackEnd by remember { mutableStateOf(true) }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(Background)
            .statusBarsPadding()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 16.dp, vertical = 10.dp)
            .padding(bottom = 90.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp)
    ) {
        // Drag Handle
        Box(
            modifier = Modifier
                .align(Alignment.CenterHorizontally)
                .width(40.dp)
                .height(4.dp)
                .clip(CircleShape)
                .background(SurfaceContainerHighest)
        )

        // Header Section
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Icon(Icons.Default.Bedtime, contentDescription = null, tint = PrimaryContainer, modifier = Modifier.size(24.dp))
                Column {
                    Text(text = "Sleep Timer & Auto-Stop", style = Typography.headlineSmall, color = OnSurface)
                    Text(text = "Automatically stop playback and conserve battery", style = Typography.bodySmall, color = OnSurfaceVariant)
                }
            }

            IconButton(onClick = onClose) {
                Icon(Icons.Default.Close, contentDescription = "Close", tint = OnSurfaceVariant)
            }
        }

        // Active Status Card with Circular Gauge
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .clip(RoundedCornerShape(16.dp))
                .background(SurfaceContainerLow)
                .padding(16.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                // Circular Countdown Gauge
                Box(
                    modifier = Modifier
                        .size(110.dp),
                    contentAlignment = Alignment.Center
                ) {
                    val progress = if (totalMs > 0 && isActive) (remainingMs.toFloat() / totalMs.toFloat()).coerceIn(0f, 1f) else 1f

                    Canvas(modifier = Modifier.fillMaxSize()) {
                        drawCircle(
                            color = SurfaceContainerHighest,
                            style = Stroke(width = 8.dp.toPx())
                        )
                        drawArc(
                            color = PrimaryContainer,
                            startAngle = -90f,
                            sweepAngle = progress * 360f,
                            useCenter = false,
                            style = Stroke(width = 8.dp.toPx(), cap = StrokeCap.Round)
                        )
                    }

                    Column(horizontalAlignment = Alignment.CenterHorizontally) {
                        val minutes = if (isActive) (remainingMs / 1000) / 60 else selectedMinutes
                        val seconds = if (isActive) (remainingMs / 1000) % 60 else 0
                        Text(
                            text = String.format("%02d:%02d", minutes, seconds),
                            style = Typography.headlineMedium,
                            fontWeight = FontWeight.Bold,
                            color = OnSurface
                        )
                        Text(
                            text = if (isActive) "REMAINING" else "READY",
                            style = Typography.labelSmall.copy(fontSize = 9.sp),
                            color = Primary
                        )
                    }
                }

                // Summary details
                Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    Box(
                        modifier = Modifier
                            .clip(RoundedCornerShape(4.dp))
                            .background(TertiaryContainer.copy(alpha = 0.2f))
                            .padding(horizontal = 6.dp, vertical = 2.dp)
                    ) {
                        Text(text = if (isActive) "● TIMER ACTIVE" else "IDLE", style = Typography.labelSmall, color = Tertiary)
                    }
                    Text(text = "Active Sleep Profile", style = Typography.headlineSmall, color = OnSurface)
                    Text(
                        text = "Stop after $selectedMinutes min • Fade volume during last 60s",
                        style = Typography.bodySmall,
                        color = OnSurfaceVariant
                    )
                    Row(horizontalArrangement = Arrangement.spacedBy(4.dp), modifier = Modifier.padding(top = 4.dp)) {
                        Box(
                            modifier = Modifier
                                .clip(RoundedCornerShape(3.dp))
                                .background(SurfaceContainerHigh)
                                .padding(horizontal = 4.dp, vertical = 2.dp)
                        ) {
                            Text(text = "AUDIO MEDIA3-CORE", style = Typography.labelSmall.copy(fontSize = 9.sp), color = Secondary)
                        }
                    }
                }
            }
        }

        // Quick Presets Grid
        Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Text(text = "QUICK PRESETS", style = Typography.labelSmall, color = OnSurfaceVariant)
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                PresetButton(title = "15 Min", subtitle = "Power Nap", isSelected = selectedMinutes == 15, onClick = { selectedMinutes = 15 }, modifier = Modifier.weight(1f))
                PresetButton(title = "30 Min", subtitle = "Deep Rest", isSelected = selectedMinutes == 30, onClick = { selectedMinutes = 30 }, modifier = Modifier.weight(1f))
                PresetButton(title = "45 Min", subtitle = "1 Episode", isSelected = selectedMinutes == 45, onClick = { selectedMinutes = 45 }, modifier = Modifier.weight(1f))
            }
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                PresetButton(title = "60 Min", subtitle = "Full Album", isSelected = selectedMinutes == 60, onClick = { selectedMinutes = 60 }, modifier = Modifier.weight(1f))
                PresetButton(title = "90 Min", subtitle = "Feature Film", isSelected = selectedMinutes == 90, onClick = { selectedMinutes = 90 }, modifier = Modifier.weight(1f))
                PresetButton(title = "Track End", subtitle = "Auto Sync", isSelected = selectedMinutes == -1, onClick = { selectedMinutes = -1 }, modifier = Modifier.weight(1f))
            }
        }

        // Custom Duration Slider
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .clip(RoundedCornerShape(16.dp))
                .background(SurfaceContainer)
                .padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                    Icon(Icons.Outlined.Tune, contentDescription = null, tint = Primary, modifier = Modifier.size(18.dp))
                    Text(text = "CUSTOM DURATION", style = Typography.labelSmall, color = OnSurface)
                }
                Text(text = "$selectedMinutes Minutes", style = Typography.labelLarge, color = Primary, fontWeight = FontWeight.Bold)
            }

            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                IconButton(
                    onClick = { if (selectedMinutes > 5) selectedMinutes -= 5 },
                    modifier = Modifier
                        .size(36.dp)
                        .clip(RoundedCornerShape(8.dp))
                        .background(SurfaceContainerHighest)
                ) {
                    Icon(Icons.Default.Remove, contentDescription = "-", tint = OnSurface)
                }

                Slider(
                    value = selectedMinutes.toFloat().coerceIn(1f, 180f),
                    onValueChange = { selectedMinutes = it.toInt() },
                    valueRange = 1f..180f,
                    colors = SliderDefaults.colors(
                        thumbColor = PrimaryContainer,
                        activeTrackColor = PrimaryContainer,
                        inactiveTrackColor = SurfaceContainerLowest
                    ),
                    modifier = Modifier.weight(1f)
                )

                IconButton(
                    onClick = { if (selectedMinutes < 180) selectedMinutes += 5 },
                    modifier = Modifier
                        .size(36.dp)
                        .clip(RoundedCornerShape(8.dp))
                        .background(SurfaceContainerHighest)
                ) {
                    Icon(Icons.Default.Add, contentDescription = "+", tint = OnSurface)
                }
            }
        }

        // Automation Behavior Toggles
        Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Text(text = "AUTOMATION BEHAVIOR", style = Typography.labelSmall, color = OnSurfaceVariant)

            ToggleRow(
                icon = Icons.Outlined.VolumeDown,
                iconTint = Secondary,
                title = "Gentle volume fade-out",
                subtitle = "Smooth decibel attenuation over the last 60 seconds",
                checked = volumeFadeOut,
                onCheckedChange = {
                    volumeFadeOut = it
                    sleepTimerManager.isVolumeFadeOutEnabled = it
                }
            )

            ToggleRow(
                icon = Icons.Outlined.PauseCircle,
                iconTint = Primary,
                title = "Pause playback",
                subtitle = "Retain media buffer instead of closing player process",
                checked = pauseInsteadOfStop,
                onCheckedChange = {
                    pauseInsteadOfStop = it
                    sleepTimerManager.isPauseInsteadOfStop = it
                }
            )

            ToggleRow(
                icon = Icons.Outlined.PlaylistPlay,
                iconTint = Tertiary,
                title = "Wait for track completion",
                subtitle = "Finish current track if timer elapses mid-stream",
                checked = waitForTrackEnd,
                onCheckedChange = {
                    waitForTrackEnd = it
                    sleepTimerManager.isWaitForTrackEnd = it
                }
            )
        }

        // Action Buttons
        Column(verticalArrangement = Arrangement.spacedBy(10.dp), modifier = Modifier.padding(top = 10.dp)) {
            Button(
                onClick = {
                    sleepTimerManager.startTimer(selectedMinutes)
                },
                colors = ButtonDefaults.buttonColors(containerColor = PrimaryContainer, contentColor = OnPrimaryContainer),
                shape = RoundedCornerShape(14.dp),
                modifier = Modifier
                    .fillMaxWidth()
                    .height(52.dp)
                    .shadow(elevation = 16.dp, shape = RoundedCornerShape(14.dp), spotColor = PrimaryContainer)
            ) {
                Icon(Icons.Default.PlayCircle, contentDescription = null, modifier = Modifier.size(22.dp))
                Spacer(modifier = Modifier.width(8.dp))
                Text(text = "Start / Reset Timer", style = Typography.headlineSmall, fontWeight = FontWeight.Bold)
            }

            if (isActive) {
                OutlinedButton(
                    onClick = { sleepTimerManager.cancelTimer() },
                    colors = ButtonDefaults.outlinedButtonColors(contentColor = Error),
                    shape = RoundedCornerShape(14.dp),
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(48.dp)
                ) {
                    Icon(Icons.Default.AlarmOff, contentDescription = null, modifier = Modifier.size(20.dp))
                    Spacer(modifier = Modifier.width(8.dp))
                    Text(text = "Cancel Active Timer", style = Typography.bodyLarge)
                }
            }
        }
    }
}

@Composable
private fun PresetButton(
    title: String,
    subtitle: String,
    isSelected: Boolean,
    onClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    val bg = if (isSelected) PrimaryContainer else SurfaceContainer
    val textColor = if (isSelected) OnPrimaryContainer else OnSurfaceVariant

    Column(
        modifier = modifier
            .clip(RoundedCornerShape(10.dp))
            .background(bg)
            .clickable { onClick() }
            .padding(vertical = 10.dp, horizontal = 6.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center
    ) {
        Text(text = title, style = Typography.labelMedium, fontWeight = FontWeight.Bold, color = textColor)
        Text(text = subtitle, style = Typography.labelSmall.copy(fontSize = 9.sp), color = textColor.copy(alpha = 0.8f))
    }
}

@Composable
private fun ToggleRow(
    icon: androidx.compose.ui.graphics.vector.ImageVector,
    iconTint: Color,
    title: String,
    subtitle: String,
    checked: Boolean,
    onCheckedChange: (Boolean) -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(SurfaceContainer)
            .clickable { onCheckedChange(!checked) }
            .padding(12.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Row(
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(10.dp),
            modifier = Modifier.weight(1f)
        ) {
            Icon(icon, contentDescription = null, tint = iconTint, modifier = Modifier.size(22.dp))
            Column {
                Text(text = title, style = Typography.bodyMedium, fontWeight = FontWeight.Medium, color = OnSurface)
                Text(text = subtitle, style = Typography.bodySmall, color = OnSurfaceVariant)
            }
        }
        Switch(
            checked = checked,
            onCheckedChange = onCheckedChange,
            colors = SwitchDefaults.colors(
                checkedThumbColor = OnPrimaryContainer,
                checkedTrackColor = PrimaryContainer,
                uncheckedTrackColor = SurfaceContainerHighest
            )
        )
    }
}
