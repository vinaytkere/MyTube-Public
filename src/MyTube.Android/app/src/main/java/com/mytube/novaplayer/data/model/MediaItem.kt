package com.mytube.novaplayer.data.model

data class MediaItem(
    val id: Long,
    val uri: String,
    val title: String,
    val durationMs: Long,
    val sizeBytes: Long,
    val isVideo: Boolean,
    val mimeType: String,
    val path: String,
    val codecBadges: List<String> = emptyList(),
    val isDownloaded: Boolean = false,
    val artistOrSubtitle: String = ""
) {
    val formattedDuration: String
        get() {
            val totalSeconds = durationMs / 1000
            val hours = totalSeconds / 3600
            val minutes = (totalSeconds % 3600) / 60
            val seconds = totalSeconds % 60
            return if (hours > 0) {
                String.format("%02d:%02d:%02d", hours, minutes, seconds)
            } else {
                String.format("%02d:%02d", minutes, seconds)
            }
        }

    val formattedSize: String
        get() {
            val mb = sizeBytes / (1024.0 * 1024.0)
            return if (mb >= 1024) {
                String.format("%.1f GB", mb / 1024.0)
            } else {
                String.format("%.0f MB", mb)
            }
        }
}
