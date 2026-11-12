package com.mytube.novaplayer.data.util

import android.content.Context
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.media.MediaMetadataRetriever
import android.net.Uri
import android.os.Build
import android.util.LruCache
import android.util.Size
import androidx.core.graphics.drawable.toBitmap
import coil.Coil
import coil.request.ImageRequest
import coil.request.SuccessResult
import com.mytube.novaplayer.data.model.MediaItem
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.File

object MediaThumbnailHelper {

    // In-memory LRU cache for 15 thumbnail bitmaps (approx 15MB total)
    private val memoryCache = object : LruCache<Long, Bitmap>(15) {
        override fun sizeOf(key: Long, value: Bitmap): Int = 1
    }

    suspend fun getThumbnailBitmap(context: Context, media: MediaItem): Bitmap? = withContext(Dispatchers.IO) {
        // Check cache first
        memoryCache.get(media.id)?.let { return@withContext it }

        var resultBitmap: Bitmap? = null

        // 1. MediaStore loadThumbnail for content:// URIs on Android 10+
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q && media.uri.startsWith("content://")) {
            try {
                val uri = Uri.parse(media.uri)
                val bm = context.contentResolver.loadThumbnail(uri, Size(512, 512), null)
                resultBitmap = if (bm.config == Bitmap.Config.HARDWARE) {
                    bm.copy(Bitmap.Config.ARGB_8888, false)
                } else {
                    bm
                }
            } catch (_: Exception) {
                // Fallback to Coil / Retriever
            }
        }

        // 2. Coil Image Loader with VideoFrameDecoder (software bitmap for IPC safety)
        if (resultBitmap == null) {
            try {
                val targetData: Any = if (media.path.isNotBlank() && File(media.path).exists()) {
                    File(media.path)
                } else {
                    Uri.parse(media.uri)
                }

                val request = ImageRequest.Builder(context)
                    .data(targetData)
                    .size(512, 512)
                    .allowHardware(false) // Must be false for Notification and MediaSession IPC
                    .build()

                val response = Coil.imageLoader(context).execute(request)
                if (response is SuccessResult) {
                    resultBitmap = response.drawable.toBitmap(512, 512, Bitmap.Config.ARGB_8888)
                }
            } catch (_: Exception) {
                // Fallback to retriever
            }
        }

        // 3. MediaMetadataRetriever fallback
        if (resultBitmap == null) {
            val retriever = MediaMetadataRetriever()
            try {
                if (media.path.isNotBlank() && File(media.path).exists()) {
                    retriever.setDataSource(media.path)
                } else {
                    retriever.setDataSource(context, Uri.parse(media.uri))
                }

                if (media.isVideo) {
                    val frame = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O_MR1) {
                        retriever.getScaledFrameAtTime(1_000_000L, MediaMetadataRetriever.OPTION_CLOSEST_SYNC, 512, 512)
                    } else {
                        retriever.getFrameAtTime(1_000_000L, MediaMetadataRetriever.OPTION_CLOSEST_SYNC)
                    }
                    if (frame != null) {
                        resultBitmap = if (frame.config == Bitmap.Config.HARDWARE) {
                            frame.copy(Bitmap.Config.ARGB_8888, false)
                        } else {
                            frame
                        }
                    }
                } else {
                    val picture = retriever.embeddedPicture
                    if (picture != null) {
                        resultBitmap = BitmapFactory.decodeByteArray(picture, 0, picture.size)
                    }
                }
            } catch (_: Exception) {
            } finally {
                try {
                    retriever.release()
                } catch (_: Exception) {}
            }
        }

        if (resultBitmap != null) {
            memoryCache.put(media.id, resultBitmap)
        }

        resultBitmap
    }
}
