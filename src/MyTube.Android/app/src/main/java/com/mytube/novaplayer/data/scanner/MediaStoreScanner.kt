package com.mytube.novaplayer.data.scanner

import android.content.ContentUris
import android.content.Context
import android.media.MediaMetadataRetriever
import android.net.Uri
import android.os.Build
import android.os.Environment
import android.provider.MediaStore
import com.mytube.novaplayer.data.model.MediaItem
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.File

class MediaStoreScanner(private val context: Context) {

    suspend fun scanAllMedia(): List<MediaItem> = withContext(Dispatchers.IO) {
        val mediaList = mutableListOf<MediaItem>()
        mediaList.addAll(scanVideos())
        mediaList.addAll(scanAudio())
        mediaList.addAll(scanDirectDirectories(mediaList))
        mediaList.sortWith(
            compareByDescending<MediaItem> { it.isVideo }
                .thenByDescending { it.id % 10_000_000_000L }
        )
        mediaList
    }

    private fun scanVideos(): List<MediaItem> {
        val list = mutableListOf<MediaItem>()
        val collection = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            MediaStore.Video.Media.getContentUri(MediaStore.VOLUME_EXTERNAL)
        } else {
            MediaStore.Video.Media.EXTERNAL_CONTENT_URI
        }

        val projection = arrayOf(
            MediaStore.Video.Media._ID,
            MediaStore.Video.Media.TITLE,
            MediaStore.Video.Media.DURATION,
            MediaStore.Video.Media.SIZE,
            MediaStore.Video.Media.MIME_TYPE,
            MediaStore.Video.Media.DATA,
            MediaStore.Video.Media.WIDTH,
            MediaStore.Video.Media.HEIGHT,
            MediaStore.Video.Media.ORIENTATION
        )

        context.contentResolver.query(
            collection,
            projection,
            null,
            null,
            "${MediaStore.Video.Media.DATE_ADDED} DESC"
        )?.use { cursor ->
            val idCol = cursor.getColumnIndexOrThrow(MediaStore.Video.Media._ID)
            val titleCol = cursor.getColumnIndexOrThrow(MediaStore.Video.Media.TITLE)
            val durCol = cursor.getColumnIndexOrThrow(MediaStore.Video.Media.DURATION)
            val sizeCol = cursor.getColumnIndexOrThrow(MediaStore.Video.Media.SIZE)
            val mimeCol = cursor.getColumnIndexOrThrow(MediaStore.Video.Media.MIME_TYPE)
            val dataCol = cursor.getColumnIndexOrThrow(MediaStore.Video.Media.DATA)
            val widthCol = cursor.getColumnIndex(MediaStore.Video.Media.WIDTH)
            val heightCol = cursor.getColumnIndex(MediaStore.Video.Media.HEIGHT)
            val orientCol = cursor.getColumnIndex(MediaStore.Video.Media.ORIENTATION)

            while (cursor.moveToNext()) {
                val id = cursor.getLong(idCol)
                val title = cursor.getString(titleCol) ?: "Video_$id"
                val duration = cursor.getLong(durCol)
                val size = cursor.getLong(sizeCol)
                val mime = cursor.getString(mimeCol) ?: "video/mp4"
                val path = cursor.getString(dataCol) ?: ""
                val width = if (widthCol != -1) cursor.getInt(widthCol) else 0
                val height = if (heightCol != -1) cursor.getInt(heightCol) else 0
                val orientation = if (orientCol != -1) cursor.getInt(orientCol) else 0
                val isRotated = orientation == 90 || orientation == 270
                val effWidth = if (isRotated) height else width
                val effHeight = if (isRotated) width else height

                val contentUri = ContentUris.withAppendedId(collection, id).toString()

                // Generate codec badges
                val badges = mutableListOf<String>()
                val maxDim = maxOf(effWidth, effHeight)
                when {
                    maxDim >= 3840 -> badges.add("4K HDR")
                    maxDim >= 1920 -> badges.add("1080p")
                    maxDim >= 1280 -> badges.add("720p")
                }
                val ext = File(path).extension.uppercase()
                if (ext.isNotEmpty()) badges.add(ext) else badges.add("MP4")

                list.add(
                    MediaItem(
                        id = id,
                        uri = contentUri,
                        title = title,
                        durationMs = duration,
                        sizeBytes = size,
                        isVideo = true,
                        mimeType = mime,
                        path = path,
                        codecBadges = badges,
                        artistOrSubtitle = "Video Stream",
                        width = effWidth,
                        height = effHeight
                    )
                )
            }
        }
        return list
    }

    private fun scanAudio(): List<MediaItem> {
        val list = mutableListOf<MediaItem>()
        val collection = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            MediaStore.Audio.Media.getContentUri(MediaStore.VOLUME_EXTERNAL)
        } else {
            MediaStore.Audio.Media.EXTERNAL_CONTENT_URI
        }

        val projection = arrayOf(
            MediaStore.Audio.Media._ID,
            MediaStore.Audio.Media.TITLE,
            MediaStore.Audio.Media.DURATION,
            MediaStore.Audio.Media.SIZE,
            MediaStore.Audio.Media.MIME_TYPE,
            MediaStore.Audio.Media.DATA,
            MediaStore.Audio.Media.ARTIST
        )

        // Include all audio files while ignoring notification/alarm/ringtone sounds safely
        val selection = "${MediaStore.Audio.Media.SIZE} > 0 AND (" +
            "${MediaStore.Audio.Media.IS_MUSIC} != 0 OR (" +
            "COALESCE(${MediaStore.Audio.Media.IS_RINGTONE}, 0) = 0 AND " +
            "COALESCE(${MediaStore.Audio.Media.IS_NOTIFICATION}, 0) = 0 AND " +
            "COALESCE(${MediaStore.Audio.Media.IS_ALARM}, 0) = 0))"

        context.contentResolver.query(
            collection,
            projection,
            selection,
            null,
            "${MediaStore.Audio.Media.DATE_ADDED} DESC"
        )?.use { cursor ->
            val idCol = cursor.getColumnIndexOrThrow(MediaStore.Audio.Media._ID)
            val titleCol = cursor.getColumnIndexOrThrow(MediaStore.Audio.Media.TITLE)
            val durCol = cursor.getColumnIndexOrThrow(MediaStore.Audio.Media.DURATION)
            val sizeCol = cursor.getColumnIndexOrThrow(MediaStore.Audio.Media.SIZE)
            val mimeCol = cursor.getColumnIndexOrThrow(MediaStore.Audio.Media.MIME_TYPE)
            val dataCol = cursor.getColumnIndexOrThrow(MediaStore.Audio.Media.DATA)
            val artistCol = cursor.getColumnIndexOrThrow(MediaStore.Audio.Media.ARTIST)

            while (cursor.moveToNext()) {
                val id = cursor.getLong(idCol)
                val title = cursor.getString(titleCol) ?: "Audio_$id"
                val duration = cursor.getLong(durCol)
                val size = cursor.getLong(sizeCol)
                val mime = cursor.getString(mimeCol) ?: "audio/mpeg"
                val path = cursor.getString(dataCol) ?: ""
                val artist = cursor.getString(artistCol) ?: "Unknown Artist"

                val contentUri = ContentUris.withAppendedId(collection, id).toString()

                val badges = mutableListOf<String>()
                val ext = File(path).extension.uppercase()
                when (ext) {
                    "FLAC" -> {
                        badges.add("FLAC 96kHz")
                        badges.add("24-BIT")
                    }
                    "WAV" -> {
                        badges.add("WAV PCM")
                        badges.add("LOSSLESS")
                    }
                    "M4A", "AAC" -> {
                        badges.add("AAC 320k")
                        badges.add("STEREO")
                    }
                    "OPUS" -> {
                        badges.add("OPUS HD")
                        badges.add("STEREO")
                    }
                    "OGG" -> {
                        badges.add("OGG VORBIS")
                        badges.add("STEREO")
                    }
                    "WMA" -> {
                        badges.add("WMA AUDIO")
                        badges.add("STEREO")
                    }
                    else -> {
                        badges.add(if (ext.isNotEmpty()) ext else "MP3")
                        badges.add("STEREO")
                    }
                }

                list.add(
                    MediaItem(
                        id = id + 10_000_000_000L,
                        uri = contentUri,
                        title = title,
                        durationMs = duration,
                        sizeBytes = size,
                        isVideo = false,
                        mimeType = mime,
                        path = path,
                        codecBadges = badges,
                        artistOrSubtitle = artist
                    )
                )
            }
        }
        return list
    }

    /**
     * Fallback direct directory scan to ensure newly downloaded files in Download/MyTube
     * and Download appear immediately even before MediaStore background scanner indexes them.
     */
    private fun scanDirectDirectories(existingItems: List<MediaItem>): List<MediaItem> {
        val directList = mutableListOf<MediaItem>()
        val existingPaths = existingItems.mapNotNull { it.path.takeIf { p -> p.isNotBlank() }?.lowercase() }.toSet()

        val directoriesToScan = listOfNotNull(
            File(Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOWNLOADS), "MyTube"),
            File(Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_MUSIC), "MyTube"),
            File(Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_MOVIES), "MyTube"),
            Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOWNLOADS)
        )

        val videoExtensions = setOf("mp4", "mkv", "webm", "avi", "mov", "flv", "3gp", "ts", "m4v", "wmv")
        val audioExtensions = setOf("mp3", "m4a", "opus", "wav", "flac", "ogg", "aac", "wma", "weba", "amr", "mka")

        for (dir in directoriesToScan) {
            if (!dir.exists() || !dir.isDirectory) continue
            val files = dir.listFiles() ?: continue
            for (file in files) {
                if (file.isDirectory || file.name.startsWith(".")) continue
                val pathLower = file.absolutePath.lowercase()
                if (existingPaths.contains(pathLower) || directList.any { it.path.equals(file.absolutePath, ignoreCase = true) }) {
                    continue
                }

                val ext = file.extension.lowercase()
                val isVideo = ext in videoExtensions
                val isAudio = ext in audioExtensions
                if (!isVideo && !isAudio) continue

                var duration = 0L
                var title = file.nameWithoutExtension
                var artist = if (isVideo) "Video File" else "Downloaded Audio"
                var fileWidth = 0
                var fileHeight = 0

                try {
                    val mmr = MediaMetadataRetriever()
                    mmr.setDataSource(file.absolutePath)
                    val durStr = mmr.extractMetadata(MediaMetadataRetriever.METADATA_KEY_DURATION)
                    if (!durStr.isNullOrBlank()) {
                        duration = durStr.toLongOrNull() ?: 0L
                    }
                    val metaTitle = mmr.extractMetadata(MediaMetadataRetriever.METADATA_KEY_TITLE)
                    if (!metaTitle.isNullOrBlank()) {
                        title = metaTitle
                    }
                    val metaArtist = mmr.extractMetadata(MediaMetadataRetriever.METADATA_KEY_ARTIST)
                    if (!metaArtist.isNullOrBlank()) {
                        artist = metaArtist
                    }
                    if (isVideo) {
                        val wStr = mmr.extractMetadata(MediaMetadataRetriever.METADATA_KEY_VIDEO_WIDTH)
                        val hStr = mmr.extractMetadata(MediaMetadataRetriever.METADATA_KEY_VIDEO_HEIGHT)
                        val rotStr = mmr.extractMetadata(MediaMetadataRetriever.METADATA_KEY_VIDEO_ROTATION)
                        val rawW = wStr?.toIntOrNull() ?: 0
                        val rawH = hStr?.toIntOrNull() ?: 0
                        val rot = rotStr?.toIntOrNull() ?: 0
                        val isRot = rot == 90 || rot == 270
                        fileWidth = if (isRot) rawH else rawW
                        fileHeight = if (isRot) rawW else rawH
                    }
                    mmr.release()
                } catch (_: Exception) {}

                val badges = mutableListOf<String>()
                if (isAudio) {
                    when (ext.uppercase()) {
                        "FLAC" -> { badges.add("FLAC 96kHz"); badges.add("24-BIT") }
                        "WAV" -> { badges.add("WAV PCM"); badges.add("LOSSLESS") }
                        "M4A", "AAC" -> { badges.add("AAC 320k"); badges.add("STEREO") }
                        "OPUS" -> { badges.add("OPUS HD"); badges.add("STEREO") }
                        "OGG" -> { badges.add("OGG VORBIS"); badges.add("STEREO") }
                        "WMA" -> { badges.add("WMA AUDIO"); badges.add("STEREO") }
                        else -> { badges.add(if (ext.isNotEmpty()) ext.uppercase() else "MP3"); badges.add("STEREO") }
                    }
                } else {
                    badges.add(ext.uppercase())
                }

                val pseudoId = (file.absolutePath.hashCode().toLong() and 0x7FFFFFFF) + 20_000_000_000L
                val uri = Uri.fromFile(file).toString()

                directList.add(
                    MediaItem(
                        id = pseudoId,
                        uri = uri,
                        title = title,
                        durationMs = duration,
                        sizeBytes = file.length(),
                        isVideo = isVideo,
                        mimeType = if (isVideo) "video/$ext" else "audio/$ext",
                        path = file.absolutePath,
                        codecBadges = badges,
                        artistOrSubtitle = artist,
                        width = fileWidth,
                        height = fileHeight
                    )
                )
            }
        }
        return directList
    }
}
