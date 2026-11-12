package com.mytube.novaplayer.data.remote

import com.google.gson.annotations.SerializedName
import okhttp3.ResponseBody
import retrofit2.Response
import retrofit2.http.*

data class DownloadRequest(
    val url: String,
    val categoryName: String? = null,
    val quality: String? = null,
    val mediaType: String = "Video", // "Video" or "Audio"
    val format: String? = "Mp4",     // "Mp4", "Mp3", "M4a", "Opus", "Wav"
    val audioBitrate: String? = null, // "320k", "256k", "192k", "128k"
    val audioSampleRate: Int? = null,
    val audioChannels: Int? = null,
    val includeSubtitles: Boolean = false,
    val subtitleLanguage: String? = null
)

data class VideoProbeResultDto(
    val title: String = "",
    val durationSeconds: Double? = null,
    val formattedDuration: String = "",
    val thumbnailUrl: String? = null,
    val qualities: List<String> = emptyList(),
    val subtitlesAvailable: Boolean = false,
    val subtitleLanguages: List<String> = emptyList()
)

data class DownloadJobDto(
    @SerializedName("id") val id: String = "",
    @SerializedName("url") val url: String = "",
    @SerializedName("title") val title: String? = null,
    @SerializedName("categoryName") val categoryName: String? = null,
    @SerializedName("status") val status: String = "Queued",
    @SerializedName("phase") val phase: String? = null,
    @SerializedName("progress") val progress: Double = 0.0,
    @SerializedName("speed") val speed: String? = null,
    @SerializedName("eta") val eta: String? = null,
    @SerializedName("errorMessage") val errorMessage: String? = null,
    @SerializedName("filePath") val filePath: String? = null,
    @SerializedName("mediaType") val mediaType: String? = null,
    @SerializedName("format") val format: String? = null,
    @SerializedName("createdAt") val createdAt: String? = null,
    @SerializedName("completedAt") val completedAt: String? = null
)

data class VideoDto(
    val id: String = "",
    @SerializedName("displayTitle") val displayTitle: String? = null,
    @SerializedName("title") val rawTitle: String? = null,
    @SerializedName("fileName") val fileName: String? = null,
    val duration: Double = 0.0,
    @SerializedName("fileSize") val fileSize: Long = 0L,
    @SerializedName("sizeBytes") val sizeBytes: Long = 0L,
    val quality: String? = null,
    val format: String? = null,
    @SerializedName("containerFormat") val containerFormat: String? = null,
    val streamUrl: String? = null,
    val downloadUrl: String? = null,
    val thumbnailUrl: String? = null,
    @SerializedName("sourceUrl") val sourceUrl: String? = null,
    @SerializedName("filePath") val filePath: String? = null
) {
    val title: String
        get() = displayTitle?.ifBlank { null } ?: rawTitle?.ifBlank { null } ?: fileName ?: "Untitled Video"

    val effectiveSize: Long
        get() = if (fileSize > 0) fileSize else sizeBytes
}

data class VideoListResponse(
    @SerializedName("items") val items: List<VideoDto>? = null,
    @SerializedName("videos") val videos: List<VideoDto>? = null,
    val totalCount: Int = 0,
    val page: Int = 1,
    val pageSize: Int = 20
) {
    val allVideos: List<VideoDto>
        get() = items ?: videos ?: emptyList()
}

data class LoginRequest(
    val username: String,
    val password: String
)

data class AuthResponse(
    val username: String? = null,
    val fullName: String? = null,
    val email: String? = null,
    val role: String? = null,
    val token: String? = null
)

interface MyTubeApiService {

    @POST("api/auth/login")
    suspend fun login(
        @Body request: LoginRequest
    ): Response<AuthResponse>

    @GET("api/download/probe")
    suspend fun probeVideo(
        @Query("url") url: String
    ): Response<VideoProbeResultDto>

    @GET("api/download/qualities")
    suspend fun getQualities(
        @Query("url") url: String
    ): Response<List<String>>

    @POST("api/download")
    suspend fun startDownload(
        @Body request: DownloadRequest
    ): Response<DownloadJobDto>

    @GET("api/download")
    suspend fun getDownloadJobs(): Response<List<DownloadJobDto>>

    @DELETE("api/download/{id}")
    suspend fun cancelDownloadJob(
        @Path("id") id: String
    ): Response<Unit>

    @GET("api/videos")
    suspend fun getVideos(
        @Query("search") search: String? = null,
        @Query("page") page: Int = 1,
        @Query("pageSize") pageSize: Int = 20
    ): Response<VideoListResponse>

    @GET("api/videos/{id}/download")
    @Streaming
    suspend fun downloadMediaFile(
        @Path("id") id: String
    ): Response<ResponseBody>

    @GET("api/download/{id}/download")
    @Streaming
    suspend fun downloadJobFile(
        @Path("id") id: String
    ): Response<ResponseBody>
}
