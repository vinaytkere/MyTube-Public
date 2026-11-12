package com.mytube.novaplayer.data.remote

import android.content.Context
import android.media.MediaScannerConnection
import android.os.Environment
import android.util.Log
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.ResponseBody
import retrofit2.Response
import java.io.File
import java.io.FileOutputStream
import java.io.InputStream

class DownloadRepository(private val context: Context) {

    private val api: MyTubeApiService
        get() = ApiClientProvider.getService()

    companion object {
        private const val TAG = "DownloadRepository"
    }

    init {
        ApiClientProvider.init(context)
    }

    suspend fun probeVideo(url: String): Result<VideoProbeResultDto> = withContext(Dispatchers.IO) {
        try {
            ApiClientProvider.ensureAuthenticated()
            val response = api.probeVideo(url)
            if (response.isSuccessful && response.body() != null) {
                Result.success(response.body()!!)
            } else {
                val err = response.errorBody()?.string() ?: "Probe failed with code ${response.code()}"
                Result.failure(Exception(err))
            }
        } catch (e: Exception) {
            Log.e(TAG, "Error probing video: ${e.message}", e)
            Result.failure(e)
        }
    }

    suspend fun getQualities(url: String): Result<List<String>> = withContext(Dispatchers.IO) {
        try {
            ApiClientProvider.ensureAuthenticated()
            val response = api.getQualities(url)
            if (response.isSuccessful && response.body() != null) {
                Result.success(response.body()!!)
            } else {
                Result.failure(Exception("Failed to fetch qualities: ${response.code()}"))
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    suspend fun startDownload(request: DownloadRequest): Result<DownloadJobDto> = withContext(Dispatchers.IO) {
        try {
            ApiClientProvider.ensureAuthenticated()
            val response = api.startDownload(request)
            if (response.isSuccessful && response.body() != null) {
                Result.success(response.body()!!)
            } else {
                val err = response.errorBody()?.string() ?: "Download start failed with code ${response.code()}"
                Result.failure(Exception(err))
            }
        } catch (e: Exception) {
            Log.e(TAG, "Error starting download: ${e.message}", e)
            Result.failure(e)
        }
    }

    suspend fun getJobs(): Result<List<DownloadJobDto>> = withContext(Dispatchers.IO) {
        try {
            ApiClientProvider.ensureAuthenticated()
            val response = api.getDownloadJobs()
            if (response.isSuccessful && response.body() != null) {
                Result.success(response.body()!!)
            } else {
                Result.failure(Exception("Failed to fetch jobs: ${response.code()}"))
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    suspend fun cancelJob(id: String): Result<Boolean> = withContext(Dispatchers.IO) {
        try {
            ApiClientProvider.ensureAuthenticated()
            val response = api.cancelDownloadJob(id)
            if (response.isSuccessful) {
                Result.success(true)
            } else {
                Result.failure(Exception("Failed to cancel job: ${response.code()}"))
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    suspend fun getServerVideos(search: String? = null): Result<List<VideoDto>> = withContext(Dispatchers.IO) {
        try {
            ApiClientProvider.ensureAuthenticated()
            val response = api.getVideos(search = search, page = 1, pageSize = 50)
            if (response.isSuccessful && response.body() != null) {
                Result.success(response.body()!!.allVideos)
            } else {
                Result.failure(Exception("Failed to fetch server videos: ${response.code()}"))
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    /**
     * Downloads the finished media from MyTube Backend to device storage and scans it into MediaStore.
     */
    suspend fun downloadMediaToDevice(
        videoIdOrJobId: String,
        title: String,
        extension: String,
        sourceUrl: String? = null,
        onProgress: (Float) -> Unit
    ): Result<File> = withContext(Dispatchers.IO) {
        try {
            ApiClientProvider.ensureAuthenticated()

            var response: Response<ResponseBody>? = null

            // 1. Try download via job download endpoint (/api/download/{id}/download)
            try {
                val jobResp = api.downloadJobFile(videoIdOrJobId)
                if (jobResp.isSuccessful && jobResp.body() != null) {
                    response = jobResp
                }
            } catch (_: Exception) {}

            // 2. Fallback to video download endpoint (/api/videos/{id}/download)
            if (response == null || !response.isSuccessful || response.body() == null) {
                try {
                    val vidResp = api.downloadMediaFile(videoIdOrJobId)
                    if (vidResp.isSuccessful && vidResp.body() != null) {
                        response = vidResp
                    }
                } catch (_: Exception) {}
            }

            // 3. Fallback: Search server videos library for matching video by sourceUrl, title, or filename
            if (response == null || !response.isSuccessful || response.body() == null) {
                try {
                    val listRes = api.getVideos(pageSize = 50)
                    val allVideos = listRes.body()?.allVideos ?: emptyList()
                    val matched = allVideos.firstOrNull { v ->
                        (!sourceUrl.isNullOrBlank() && v.sourceUrl?.trim()?.equals(sourceUrl.trim(), ignoreCase = true) == true) ||
                        (!sourceUrl.isNullOrBlank() && v.sourceUrl?.contains(sourceUrl.substringAfterLast("/")) == true) ||
                        (v.title.isNotBlank() && title.contains(v.title, ignoreCase = true)) ||
                        (v.fileName?.isNotBlank() == true && title.contains(v.fileName.substringBeforeLast("."), ignoreCase = true))
                    }
                    if (matched != null && matched.id.isNotBlank()) {
                        val matchedResp = api.downloadMediaFile(matched.id)
                        if (matchedResp.isSuccessful && matchedResp.body() != null) {
                            response = matchedResp
                        }
                    }
                } catch (_: Exception) {}
            }

            if (response == null || !response.isSuccessful || response.body() == null) {
                val code = response?.code() ?: 404
                return@withContext Result.failure(Exception("File download failed with code $code (Check server connection or wait until job is finished)"))
            }

            val body = response.body()!!
            val safeTitle = title.replace(Regex("[^a-zA-Z0-9._ -]"), "_").trim().ifEmpty { "download_$videoIdOrJobId" }
            
            val disposition = response.headers()["Content-Disposition"]
            val contentType = response.headers()["Content-Type"]
            val headerFileName = disposition?.substringAfter("filename=", "")?.trim('"', ' ', '\'')
            val headerExt = headerFileName?.substringAfterLast('.', "")

            val cleanExt = when {
                !headerExt.isNullOrBlank() -> headerExt.lowercase()
                contentType?.contains("audio/mpeg") == true -> "mp3"
                contentType?.contains("audio/mp4") == true || contentType?.contains("audio/x-m4a") == true -> "m4a"
                contentType?.contains("audio/opus") == true || contentType?.contains("audio/ogg") == true -> "opus"
                contentType?.contains("audio/wav") == true || contentType?.contains("audio/x-wav") == true -> "wav"
                contentType?.contains("audio/flac") == true -> "flac"
                contentType?.contains("video/webm") == true -> "webm"
                contentType?.contains("video/x-matroska") == true -> "mkv"
                contentType?.contains("video/mp4") == true -> "mp4"
                extension.isNotBlank() -> extension.trimStart('.').lowercase()
                else -> "mp4"
            }
            val fileName = "$safeTitle.$cleanExt"

            val mimeType = when (cleanExt) {
                "mp3" -> "audio/mpeg"
                "m4a", "aac" -> "audio/mp4"
                "opus" -> "audio/opus"
                "wav" -> "audio/wav"
                "flac" -> "audio/flac"
                "ogg" -> "audio/ogg"
                "wma" -> "audio/x-ms-wma"
                "webm" -> if (cleanExt in listOf("mp3", "m4a", "opus", "wav")) "audio/webm" else "video/webm"
                "mkv" -> "video/x-matroska"
                "mp4" -> "video/mp4"
                "avi" -> "video/x-msvideo"
                "mov" -> "video/quicktime"
                else -> "application/octet-stream"
            }

            var inputStream: InputStream? = null
            var outputStream: java.io.OutputStream? = null
            var targetFile: File? = null

            try {
                val totalLength = body.contentLength()
                val stream = body.byteStream()
                inputStream = stream

                // Strategy 1: Standard public Downloads/MyTube folder
                val targetDir = File(
                    Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOWNLOADS),
                    "MyTube"
                )

                try {
                    if (!targetDir.exists()) {
                        targetDir.mkdirs()
                    }
                    val file = File(targetDir, fileName)
                    outputStream = FileOutputStream(file)
                    targetFile = file
                } catch (_: Exception) {
                    // Strategy 2: MediaStore insert for Android 10+ (Scoped Storage without permissions)
                    if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.Q) {
                        try {
                            val values = android.content.ContentValues().apply {
                                put(android.provider.MediaStore.MediaColumns.DISPLAY_NAME, fileName)
                                put(android.provider.MediaStore.MediaColumns.MIME_TYPE, mimeType)
                                put(android.provider.MediaStore.MediaColumns.RELATIVE_PATH, "${Environment.DIRECTORY_DOWNLOADS}/MyTube")
                            }
                            val uri = context.contentResolver.insert(android.provider.MediaStore.Downloads.EXTERNAL_CONTENT_URI, values)
                            if (uri != null) {
                                outputStream = context.contentResolver.openOutputStream(uri)
                                targetFile = File(targetDir, fileName)
                            }
                        } catch (_: Exception) {}
                    }

                    // Strategy 3: Dedicated app storage fallback (always guaranteed writable)
                    if (outputStream == null) {
                        val fallbackDir = File(context.getExternalFilesDir(Environment.DIRECTORY_DOWNLOADS), "MyTube").apply {
                            if (!exists()) mkdirs()
                        }
                        val file = File(fallbackDir, fileName)
                        outputStream = FileOutputStream(file)
                        targetFile = file
                    }
                }

                val safeOutputStream = outputStream ?: return@withContext Result.failure(Exception("Cannot create writable file in storage."))

                val buffer = ByteArray(8192)
                var bytesRead: Int
                var totalBytesRead = 0L

                while (stream.read(buffer).also { bytesRead = it } != -1) {
                    safeOutputStream.write(buffer, 0, bytesRead)
                    totalBytesRead += bytesRead
                    if (totalLength > 0) {
                        val progress = totalBytesRead.toFloat() / totalLength.toFloat()
                        onProgress(progress)
                    }
                }
                safeOutputStream.flush()

                // Scan into Android MediaStore for immediate discovery
                targetFile?.let { f ->
                    if (f.exists()) {
                        val scanDeferred = kotlinx.coroutines.CompletableDeferred<Boolean>()
                        MediaScannerConnection.scanFile(
                            context,
                            arrayOf(f.absolutePath),
                            arrayOf(mimeType)
                        ) { path, uri ->
                            Log.d(TAG, "Scanned $path into MediaStore: $uri")
                            scanDeferred.complete(true)
                        }

                        try {
                            kotlinx.coroutines.withTimeoutOrNull(2500L) {
                                scanDeferred.await()
                            }
                        } catch (scanEx: Exception) {
                            Log.w(TAG, "MediaScanner note: ${scanEx.message}")
                        }
                    }
                }

                Result.success(targetFile ?: File(targetDir, fileName))
            } finally {
                try { inputStream?.close() } catch (_: Exception) {}
                try { outputStream?.close() } catch (_: Exception) {}
            }
        } catch (e: Exception) {
            Log.e(TAG, "Failed to download media file to device: ${e.message}", e)
            Result.failure(e)
        }
    }
}
