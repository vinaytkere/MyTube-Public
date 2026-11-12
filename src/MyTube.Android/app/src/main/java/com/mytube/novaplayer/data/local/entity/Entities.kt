package com.mytube.novaplayer.data.local.entity

import androidx.room.Entity
import androidx.room.PrimaryKey

@Entity(tableName = "media_items")
data class MediaEntity(
    @PrimaryKey val id: Long,
    val uri: String,
    val title: String,
    val durationMs: Long,
    val sizeBytes: Long,
    val isVideo: Boolean,
    val mimeType: String,
    val path: String,
    val codecBadgesJoined: String, // Comma-separated or JSON
    val isDownloaded: Boolean,
    val artistOrSubtitle: String
)

@Entity(tableName = "playlists")
data class PlaylistEntity(
    @PrimaryKey(autoGenerate = true) val id: Long = 0,
    val name: String,
    val createdAtMs: Long = System.currentTimeMillis()
)

@Entity(
    tableName = "playlist_tracks",
    primaryKeys = ["playlistId", "mediaId"]
)
data class PlaylistTrackEntity(
    val playlistId: Long,
    val mediaId: Long,
    val sortOrder: Int
)

fun com.mytube.novaplayer.data.model.MediaItem.toEntity() = MediaEntity(
    id = id,
    uri = uri,
    title = title,
    durationMs = durationMs,
    sizeBytes = sizeBytes,
    isVideo = isVideo,
    mimeType = mimeType,
    path = path,
    codecBadgesJoined = codecBadges.joinToString(","),
    isDownloaded = isDownloaded,
    artistOrSubtitle = artistOrSubtitle
)

fun MediaEntity.toMediaItem() = com.mytube.novaplayer.data.model.MediaItem(
    id = id,
    uri = uri,
    title = title,
    durationMs = durationMs,
    sizeBytes = sizeBytes,
    isVideo = isVideo,
    mimeType = mimeType,
    path = path,
    codecBadges = if (codecBadgesJoined.isEmpty()) emptyList() else codecBadgesJoined.split(","),
    isDownloaded = isDownloaded,
    artistOrSubtitle = artistOrSubtitle
)
